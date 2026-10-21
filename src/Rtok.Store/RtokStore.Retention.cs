using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Hook bodies cleared per write transaction, so a first run over a large store
    /// never holds the write lock for the whole clear.</summary>
    internal const int HookBodyBatch = 5000;

    private const long SecondsPerDay = 86_400;

    /// <summary>Retention on its own store, then the one-time switch of an older file to
    /// incremental vacuum (retention first, so the vacuum rewrites the smaller file). Both retry
    /// on the next start: a failure comes back as a warning, never as an exception.</summary>
    public static IReadOnlyList<StoreWarning> Housekeeping(
        StoreOptions options,
        int retainCallsDays,
        int retainHookBodiesDays
    )
    {
        static StoreWarning Warn(string what, Exception e) =>
            new(what, $"{what} skipped until next start: {e.Message}");
        RtokStore store;
        try
        {
            store = Open(options);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return [Warn("retention", e)];
        }
        using (store)
        {
            try
            {
                store.RunRetention(retainCallsDays, retainHookBodiesDays);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return [Warn("retention", e)];
            }
            try
            {
                store.ConvertToIncrementalVacuum();
                return [];
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return [Warn("vacuum", e)];
            }
        }
    }

    /// <summary>Applies <c>core.retain_calls_days</c> and <c>core.retain_hook_bodies_days</c>
    /// (0 keeps forever), drops session-field rows nothing references and the symbol rows of
    /// roots that are gone, then returns the freed pages. Returns the <c>calls</c> rows purged.</summary>
    public int RunRetention(int retainCallsDays, int retainHookBodiesDays)
    {
        var purged = PurgeCallsOlderThan(retainCallsDays);
        ClearHookBodiesOlderThan(retainHookBodiesDays);
        // After both: either can leave a session-fields row unreferenced.
        using (var scope = EnterMaintenance())
        {
            var db = scope.Db;
            db.HookSessions.Where(h => !db.CallIo.Any(io => io.HookSessionId == h.Id))
                .ExecuteDelete();
            scope.Commit();
        }
        DropDeadSymbolRoots(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        using (EnterMaintenance())
        {
            Pragmas.IncrementalVacuum(_connection);
        }
        return purged;
    }

    /// <summary>The archive files <see cref="PurgeCallsOlderThan"/> would delete for
    /// <paramref name="retainCallsDays"/>. Reads only.</summary>
    public IReadOnlyList<string> ArchivesPendingRetention(int retainCallsDays)
    {
        if (retainCallsDays <= 0)
        {
            return [];
        }
        using var scope = Enter();
        return [.. DoomedArchives(scope.Db, Cutoff(retainCallsDays)).Select(a => a.Path)];
    }

    /// <summary>Drops <c>calls</c> older than <paramref name="days"/> with the rows that only
    /// describe them (<c>logs</c>, <c>tokens</c>, <c>call_io</c>) and the archives only they
    /// referenced. Ledger rows that point at a dropped call (<c>usage</c>, <c>measurements</c>,
    /// a newer child call) stay, detached: a saving is not deleted with its call. One
    /// transaction and one cutoff; the archive files go after the commit.</summary>
    public int PurgeCallsOlderThan(int days)
    {
        if (days <= 0)
        {
            return 0;
        }
        var cutoff = Cutoff(days);
        int purged;
        List<(string Id, string Path)> doomed;
        using (var scope = EnterMaintenance())
        {
            var db = scope.Db;
            doomed = DoomedArchives(db, cutoff);
            long? detached = null;
            db.Logs.Where(l =>
                    l.Ts < cutoff || db.Calls.Any(c => c.Id == l.CallId && c.Ts < cutoff)
                )
                .ExecuteDelete();
            db.Tokens.Where(t =>
                    t.Ts < cutoff || db.Calls.Any(c => c.Id == t.CallId && c.Ts < cutoff)
                )
                .ExecuteDelete();
            db.CallIo.Where(io => db.Calls.Any(c => c.Id == io.CallId && c.Ts < cutoff))
                .ExecuteDelete();
            db.Usage.Where(u => db.Calls.Any(c => c.Id == u.CallId && c.Ts < cutoff))
                .ExecuteUpdate(s => s.SetProperty(u => u.CallId, _ => detached));
            db.Measurements.Where(m => db.Calls.Any(c => c.Id == m.CallId && c.Ts < cutoff))
                .ExecuteUpdate(s => s.SetProperty(m => m.CallId, _ => detached));
            db.Calls.Where(c => db.Calls.Any(old => old.Id == c.ParentId && old.Ts < cutoff))
                .ExecuteUpdate(s => s.SetProperty(c => c.ParentId, _ => detached));
            foreach (var (id, _) in doomed)
            {
                var archiveId = id;
                string? unlinked = null;
                db.ArchiveDecisions.Where(d => d.ArchiveId == archiveId).ExecuteDelete();
                db.ArchiveSummaries.Where(s => s.ArchiveId == archiveId).ExecuteDelete();
                db.ReadCache.Where(r => r.ArchiveId == archiveId)
                    .ExecuteUpdate(s => s.SetProperty(r => r.ArchiveId, unlinked));
                db.Archive.Where(a => a.Id == archiveId).ExecuteDelete();
            }
            purged = db.Calls.Where(c => c.Ts < cutoff).ExecuteDelete();
            scope.Commit();
        }
        foreach (var (_, path) in doomed)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The row is gone; a file left behind is only disk.
            }
        }
        return purged;
    }

    /// <summary>Clears the stdin bodies (and session fields) of hook <c>call_io</c> rows older
    /// than <paramref name="days"/> (0 = never); the <c>calls</c> row, sizes, shas and archive
    /// columns stay. Returns the rows cleared.</summary>
    public int ClearHookBodiesOlderThan(int days) => ClearHookBodies(days, HookBodyBatch);

    /// <summary><see cref="ClearHookBodiesOlderThan"/> with one transaction per
    /// <paramref name="batch"/> rows and the lock released in between.</summary>
    internal int ClearHookBodies(int days, int batch)
    {
        if (days <= 0)
        {
            return 0;
        }
        var cutoff = Cutoff(days);
        var total = 0;
        while (true)
        {
            using var scope = EnterMaintenance();
            var db = scope.Db;
            var size = batch;
            (string? text, byte[]? raw, long? session) = (null, null, null);
            // The batch is the oldest matching rows up to an id: a list parameter would need EF's
            // reflection-built JSON reader, which NativeAOT lacks.
            var last = db
                .CallIo.Where(io =>
                    db.Calls.Any(c => c.Id == io.CallId && c.Kind == "hook" && c.Ts < cutoff)
                    && (
                        io.RequestJson != null
                        || io.ResponseJson != null
                        || io.RequestRaw != null
                        || io.ResponseRaw != null
                    )
                )
                .Select(io => (long?)io.CallId)
                .Order()
                .Take(size)
                .Max();
            if (last is not { } upTo)
            {
                return total;
            }
            total += db
                .CallIo.Where(io =>
                    io.CallId <= upTo
                    && db.Calls.Any(c => c.Id == io.CallId && c.Kind == "hook" && c.Ts < cutoff)
                    && (
                        io.RequestJson != null
                        || io.ResponseJson != null
                        || io.RequestRaw != null
                        || io.ResponseRaw != null
                    )
                )
                // Every value is a null in the same lambda form: EF's precompiler (10.0.13) reads
                // several setters' captured values in reverse order (research.md F), which a
                // set of nulls cannot tell from the right one.
                .ExecuteUpdate(s =>
                    s.SetProperty(io => io.RequestJson, _ => text)
                        .SetProperty(io => io.ResponseJson, _ => text)
                        .SetProperty(io => io.RequestRaw, _ => raw)
                        .SetProperty(io => io.ResponseRaw, _ => raw)
                        // The session fields belong to the body; the row is swept once unreferenced.
                        .SetProperty(io => io.HookSessionId, _ => session)
                );
            scope.Commit();
        }
    }

    /// <summary>Drops the code-graph rows of every root that is no longer a directory (a removed
    /// worktree or clone) or may never be one (the file-system root, <paramref name="home"/>).
    /// The empty root is a placeholder and stays. Returns the roots dropped.</summary>
    public int DropDeadSymbolRoots(string? home)
    {
        using var scope = EnterMaintenance();
        var db = scope.Db;
        var roots = db
            .Symbols.Select(s => s.Root)
            .Distinct()
            .ToList()
            .Concat(db.SymbolStale.Select(s => s.Root).Distinct().ToList())
            .Concat(db.Extractor.Select(e => e.Root).Distinct().ToList())
            .Distinct(StringComparer.Ordinal)
            .Where(r => r.Length > 0 && (!Directory.Exists(r) || IsUnwalkableRoot(r, home)))
            .ToList();
        foreach (var root in roots)
        {
            var dead = root;
            db.Symbols.Where(s => s.Root == dead).ExecuteDelete();
            db.SymbolStale.Where(s => s.Root == dead).ExecuteDelete();
            db.Extractor.Where(e => e.Root == dead).ExecuteDelete();
        }
        scope.Commit();
        return roots.Count;
    }

    /// <summary>Makes a store created without <c>auto_vacuum = INCREMENTAL</c> shrink on delete.
    /// The mode changes only through a <c>VACUUM</c>, which rewrites the file and needs free disk
    /// its size. Returns whether it converted anything.</summary>
    public bool ConvertToIncrementalVacuum()
    {
        using var scope = Enter();
        Pragmas.BusyTimeout(_connection, _options.MaintenanceBusy);
        try
        {
            if (Pragmas.AutoVacuum(_connection) == 2)
            {
                return false;
            }
            Pragmas.ConvertToIncrementalVacuum(_connection);
            return true;
        }
        finally
        {
            Pragmas.BusyTimeout(_connection, _options.Busy);
        }
    }

    private long Cutoff(int days) => Now - days * SecondsPerDay;

    /// <summary>Archives whose every <c>call_io</c> reference is older than the cutoff and that
    /// no decision or read-cache row holds: what a purge at that cutoff deletes.</summary>
    private static List<(string Id, string Path)> DoomedArchives(RtokDbContext context, long cutoff)
    {
        var db = context;
        var before = cutoff;
        return
        [
            .. db
                .Archive.Where(a =>
                    db.CallIo.Any(io =>
                        (io.RequestArchive == a.Id || io.ResponseArchive == a.Id)
                        && db.Calls.Any(c => c.Id == io.CallId && c.Ts < before)
                    )
                    && !db.CallIo.Any(io =>
                        (io.RequestArchive == a.Id || io.ResponseArchive == a.Id)
                        && db.Calls.Any(c => c.Id == io.CallId && c.Ts >= before)
                    )
                    && !db.ArchiveDecisions.Any(d => d.ArchiveId == a.Id)
                    && !db.ReadCache.Any(r => r.ArchiveId == a.Id)
                )
                .ToList()
                .Select(a => (a.Id, a.Path)),
        ];
    }

    /// <summary>The file-system root or the home directory: never a project root.</summary>
    private static bool IsUnwalkableRoot(string root, string? home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (
            string.Equals(
                full,
                Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? ""),
                StringComparison.Ordinal
            )
            || full.Length == 0
        )
        {
            return true;
        }
        return home is not null
            && string.Equals(
                full,
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(home)),
                StringComparison.Ordinal
            );
    }
}
