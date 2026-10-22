using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Archives <paramref name="body"/> under <see cref="StoreOptions.ArchiveDir"/>:
    /// the file first (temp + rename, content-addressed), then its row. The same bytes twice are
    /// one file and one row; the first (session, agent) keeps it. Returns the full id, the
    /// body's lowercase hex SHA-256. A failed insert removes the file only when this call made it
    /// and no row references it.</summary>
    public string PutArchive(string session, ReadOnlySpan<byte> body, string? agentId = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var dir =
            _options.ArchiveDir
            ?? throw new InvalidOperationException("no archive directory: set [core] archive_dir");
        var id = StoreFiles.Sha256Hex(body);
        var (path, created) = StoreFiles.WriteArchive(dir, id, body);
        try
        {
            using var scope = EnterWrite();
            var db = scope.Db;
            var archiveId = id;
            if (!db.Archive.Any(a => a.Id == archiveId))
            {
                db.Archive.Add(
                    new ArchiveEntry
                    {
                        Id = id,
                        Ts = Now,
                        Session = session,
                        Bytes = body.Length,
                        Path = path,
                        Sha256 = id,
                        AgentId = agentId,
                    }
                );
                db.SaveChanges();
            }
            scope.Commit();
        }
        catch
        {
            if (created)
            {
                RemoveUnreferenced(id, path);
            }
            throw;
        }
        return id;
    }

    /// <summary>The bytes archived as <paramref name="id"/>, or <see langword="null"/> when no
    /// row has that id or its file is gone. The live <see cref="StoreOptions.ArchiveDir"/> comes
    /// first; the stored path is the fallback for rows written under an older directory.</summary>
    public byte[]? GetArchive(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        string? stored;
        using (var scope = Enter())
        {
            var db = scope.Db;
            var archiveId = id;
            stored = db.Archive.Where(a => a.Id == archiveId).Select(a => a.Path).FirstOrDefault();
        }
        if (stored is null)
        {
            return null;
        }
        List<string> paths = [];
        if (_options.ArchiveDir is { } dir)
        {
            paths.Add(StoreFiles.ArchivePath(dir, id));
        }
        if (!paths.Contains(stored, StringComparer.Ordinal))
        {
            paths.Add(stored);
        }
        foreach (var path in paths)
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                // Next candidate.
            }
        }
        return null;
    }

    /// <summary>Up to <paramref name="limit"/> archive ids starting with the lowercase hex
    /// <paramref name="prefix"/>, in id order. A range compare on the key rather than
    /// <c>LIKE</c>: byte-exact and served by the primary key index.</summary>
    public IReadOnlyList<string> ArchiveIdsWithPrefix(string prefix, int limit)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.Length == 0 || !prefix.All(char.IsAsciiHexDigitLower))
        {
            return [];
        }
        using var scope = Enter();
        var db = scope.Db;
        var low = prefix;
        // 'g' sorts after every lowercase hex digit, so [low, high) is exactly the prefix.
        var high = prefix + "g";
        var take = limit;
#pragma warning disable CA1309, CA1310 // EF translates only the plain compare; SQLite compares BINARY.
        return db
            .Archive.Where(a => string.Compare(a.Id, low) >= 0 && string.Compare(a.Id, high) < 0)
            .OrderBy(a => a.Id)
            .Select(a => a.Id)
            .Take(take)
            .ToList();
#pragma warning restore CA1309, CA1310
    }

    /// <summary>Persists the pointer that replaced a tool result. First writer wins: a stored
    /// decision never changes, because a pointer that moves between turns costs a cache miss on
    /// every one of them.</summary>
    public void PutArchiveDecision(
        string session,
        string toolUseId,
        string archiveId,
        string pointerText
    )
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var s = session;
        var t = toolUseId;
        if (db.ArchiveDecisions.Any(d => d.Session == s && d.ToolUseId == t))
        {
            return;
        }
        db.ArchiveDecisions.Add(
            new ArchiveDecision
            {
                Session = session,
                ToolUseId = toolUseId,
                ArchiveId = archiveId,
                Pointer = pointerText,
                Ts = Now,
            }
        );
        db.SaveChanges();
        scope.Commit();
    }

    /// <summary>Any pointer text for <paramref name="archiveId"/>, by archive id alone: the
    /// expander (CLI session <c>expand</c>, an MCP session) never shares a session with the
    /// proxy that wrote the decision.</summary>
    public string? LivePointer(string archiveId)
    {
        using var scope = Enter();
        var db = scope.Db;
        var id = archiveId;
        return db
            .ArchiveDecisions.Where(d => d.ArchiveId == id)
            .OrderBy(d => d.ToolUseId)
            .ThenBy(d => d.Session)
            .Select(d => d.Pointer)
            .FirstOrDefault();
    }

    /// <summary>Freezes every decision pointing at <paramref name="archiveId"/> (in any session)
    /// and, when that changed a row, stores <paramref name="measurement"/>: both in one immediate
    /// transaction, so a failed insert leaves nothing frozen. Returns how many decisions
    /// froze.</summary>
    public int FreezeRecorded(string archiveId, string session, MeasurementInput measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        using var scope = EnterWrite();
        var db = scope.Db;
        var id = archiveId;
        long? now = Now;
        var n = db
            .ArchiveDecisions.Where(d => d.ArchiveId == id && d.ExpandedTs == null)
            .ExecuteUpdate(s => s.SetProperty(d => d.ExpandedTs, _ => now));
        if (n > 0)
        {
            db.Measurements.Add(MeasurementRow(session, measurement, onceKey: null));
            db.SaveChanges();
        }
        scope.Commit();
        return n;
    }
}
