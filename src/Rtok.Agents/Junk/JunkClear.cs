using System.Data.Common;
using System.Diagnostics;
using System.Text;
using Pyrlyn.ChangePreview;
using Pyrlyn.FsKit;
using Rtok.Config;

namespace Rtok.Agents.Junk;

/// <summary>
/// <c>rtok agents junk clear</c>: the plan is the <c>list</c> report's own items cut by the
/// filter, and each item is checked again right before it goes. Without a selecting flag only
/// rtok's own logs and archives go.
/// </summary>
public static class JunkClear
{
    /// <summary>Why an item of a running host stays.</summary>
    public const string Running = "agent running";

    /// <summary>Kinds a running agent may be writing right now; a §22 or extra path joins them.</summary>
    private static readonly string[] LiveKinds = ["temp", "locks", "swap", "index"];

    /// <summary>The kinds that keep their top folder and its <c>CACHEDIR.TAG</c>: an app may expect them.</summary>
    private static readonly string[] KeepTop = ["cache", "build", "index"];

    /// <summary>An item touched this recently is in use, whatever the scan said.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(60);

    /// <summary>Archive retention: <c>--older-than</c> only widens <c>core.retain_calls_days</c>, and 0 (keep forever) stays 0.</summary>
    public static uint RetentionDays(RtokConfig config, JunkFilter filter)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(filter);
        var days = config.Core.RetainCallsDays;
        var floor = filter.OlderThan is { } o
            ? (uint)Math.Min(Math.Ceiling(o.TotalDays), uint.MaxValue)
            : 0;
        return days == 0 ? 0 : Math.Max(days, floor);
    }

    /// <summary>
    /// The paths <c>clear</c> never removes nor empties a folder around, real: the store with its
    /// <c>-wal</c> and <c>-shm</c>, every host's config files rtok edits (<paramref name="markers"/>)
    /// and the settings and credential files of the §22 map.
    /// </summary>
    public static IReadOnlyList<string> Protected(
        RtokConfig config,
        JunkRoots roots,
        IEnumerable<string> markers
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(markers);
        var db = config.Core.DbPath;
        return
        [
            .. new[] { db, db + "-wal", db + "-shm" }
                .Concat(markers)
                .Concat(JunkMap.Protected.Select(roots.Resolve))
                .Select(FsPath.Resolved)
                .Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>The plan, and with <paramref name="yes"/> the removal.</summary>
    public static JunkCleared Run(JunkClearOptions o, bool yes)
    {
        ArgumentNullException.ThrowIfNull(o);
        var items = Plan(o);
        if (o.Only is { } only)
        {
            items = [.. items.Where(p => only.Contains(p.Path, StringComparer.Ordinal))];
        }
        foreach (var p in items.Where(p => p.Planned))
        {
            p.Files = FsTree.Usage(p.Path, JunkContext.ScanLimit).Files;
        }
        if (yes)
        {
            Apply(o, items);
        }
        return JunkCleared.Of(items, yes);
    }

    /// <summary>Every counted, documented item of the report the filter wants, each path once (a shared folder goes once).</summary>
    public static IReadOnlyList<JunkPlanned> Plan(JunkClearOptions o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var f = o.Filter.OwnOnly
            ? o.Filter with
            {
                Agents = ["rtok"],
                Kinds = ["log", "archive"],
            }
            : o.Filter;
        var now = o.View.Now;
        bool Old(string path) =>
            f.OlderThan is not { } age
            || (JunkTree.Newest(path) is (true, var t) && (t is null || now - t >= age));
        var plan = new List<JunkPlanned>();
        foreach (var a in o.Report.Agents.Where(a => f.WantsAgent(a.Name)))
        {
            if (!a.Host)
            {
                var own = JunkScan
                    .Own(o.Config, o.Store, (int)Math.Min(RetentionDays(o.Config, f), int.MaxValue))
                    .Where(x =>
                        f.WantsKind(x.Kind, "safe")
                        && x.Bytes >= f.MinSize
                        && (x.Kind is "archive" || Old(x.Path))
                    );
                plan.AddRange(
                    own.Select(x =>
                        Planned(
                            o.View,
                            new(a.Name, x.Kind, x.Path, x.Bytes, JunkEvidence.RtokOwn, null, x.Note)
                        )
                    )
                );
            }
            bool? live = null;
            foreach (
                var i in a.Items.Where(i =>
                    i.Counted
                    && JunkEvidence.Documented(i.Evidence)
                    && i.Bytes >= f.MinSize
                    && f.WantsKind(i.Kind, i.Class)
                    && Old(i.Path)
                )
            )
            {
                // A path the user named is no more known to tolerate a running agent than §22's.
                var sensitive =
                    LiveKinds.Contains(i.Kind, StringComparer.Ordinal)
                    || i.Evidence is JunkEvidence.Section22 or JunkEvidence.Extra;
                var busy = a.Host && sensitive && (live ??= o.Running(a.Name));
                plan.Add(
                    Planned(
                        o.View,
                        new(a.Name, i.Kind, i.Path, i.Bytes, i.Evidence, busy ? Running : null, "")
                    )
                );
            }
        }
        return Settled(plan);
    }

    private sealed record Pick(
        string Agent,
        string Kind,
        string Path,
        long Bytes,
        string Evidence,
        string? Kept,
        string Why
    );

    private static JunkPlanned Planned(JunkView view, Pick x)
    {
        var last = JunkTree.Newest(x.Path).Newest;
        return new()
        {
            Agent = x.Agent,
            Kind = x.Kind,
            Path = x.Path,
            Bytes = x.Bytes,
            LastUsed = last,
            Reason = x.Why.Length > 0 ? x.Why : view.Reason(x.Kind, x.Evidence, last),
            Planned = x.Kept is null,
            Action = x.Kept is null ? "clear" : "skip",
            Note = x.Kept ?? "",
            Evidence = x.Evidence,
            Real = JunkTree.Real(x.Path),
        };
    }

    /// <summary>Lockfile holders and paths a running agent shares stay; empty folders and repeats drop.</summary>
    private static List<JunkPlanned> Settled(List<JunkPlanned> plan)
    {
        // A lockfile inside a folder stays, so the folder does too.
        foreach (var p in plan.Where(p => p.Planned))
        {
            if (HoldsLockfile(p) is { } why)
            {
                p.Planned = false;
                p.Skip(why, false);
            }
        }
        // A folder shared with a running agent stays, whichever owner was listed first.
        var busyPaths = plan.Where(p => !p.Planned)
            .Select(p => p.Path)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // An emptied folder keeps its own block on most filesystems, so its size never reaches 0;
        // with nothing left inside it is not worth planning again.
        return
        [
            .. plan.Where(p =>
                (!p.Planned || !busyPaths.Contains(p.Path))
                && !IsEmptyDirectory(p.Path)
                && seen.Add(p.Path)
            ),
        ];
    }

    private static bool IsEmptyDirectory(string path)
    {
        try
        {
            return Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// A package-manager lockfile at any depth under <paramref name="dir"/>, relative to it; a link
    /// is never entered. The error when the walk could not finish: an unknown answer must not read as none.
    /// </summary>
    internal static (string? Lock, string? Error) LockfileUnder(string dir, TimeSpan limit)
    {
        var clock = Stopwatch.StartNew();
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(dir));
        while (stack.Count > 0)
        {
            if (clock.Elapsed >= limit)
            {
                return (null, $"lockfile scan of {dir} timed out");
            }
            var d = stack.Pop();
            FileSystemInfo[] entries;
            try
            {
                entries = d.GetFileSystemInfos();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return (null, $"cannot read {d.FullName}: {e.Message}");
            }
            foreach (var e in entries)
            {
                if (JunkKinds.PackageLocks.Contains(e.Name, StringComparer.Ordinal))
                {
                    return (Path.GetRelativePath(dir, e.FullName), null);
                }
                if (e is DirectoryInfo sub && e.LinkTarget is null)
                {
                    stack.Push(sub);
                }
            }
        }
        return (null, null);
    }

    /// <summary>
    /// Why removing <paramref name="p"/> would also take a lockfile, or could not rule it out. A
    /// worktree's lockfiles are its own and an archive is the store's retention: neither is walked.
    /// </summary>
    private static string? HoldsLockfile(JunkPlanned p)
    {
        if (p.Kind is "archive" or "stale-worktrees" || !JunkTree.IsDirectory(p.Path))
        {
            return null;
        }
        return LockfileUnder(p.Path, JunkContext.ScanLimit) switch
        {
            (_, { } error) => $"{error}: not cleared",
            ({ } lockfile, _) => $"holds a package-manager lockfile: {lockfile}",
            _ => null,
        };
    }

    /// <summary>Why <paramref name="p"/> must stay now, and whether that is a failure ("already gone" is not).</summary>
    internal static (string Why, bool Failed)? Recheck(
        JunkPlanned p,
        IReadOnlyList<string> keep,
        DateTime now
    )
    {
        try
        {
            _ = File.GetAttributes(p.Path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            if (!JunkTree.IsSymlink(p.Path))
            {
                return ("already gone", false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ($"cannot check it: {e.Message}", true);
        }
        var here = JunkTree.Real(p.Path);
        var why = Refusal(p, keep, here, now);
        return why is null ? null : (why, true);
    }

    private static string? Refusal(
        JunkPlanned p,
        IReadOnlyList<string> keep,
        string here,
        DateTime now
    )
    {
        if (JunkTree.IsSymlink(p.Path))
        {
            return "a symlink: never followed";
        }
        if (!string.Equals(here, p.Real, StringComparison.Ordinal))
        {
            return "changed since plan: its real path moved";
        }
        if (
            JunkKinds.PackageLocks.Contains(Path.GetFileName(p.Path), StringComparer.Ordinal)
            || keep.Any(k => JunkTree.Under(k, here))
        )
        {
            return "is or holds a protected file (store, settings, lockfile)";
        }
        if (HoldsLockfile(p) is { } held)
        {
            return held;
        }
        if (p.Evidence is JunkEvidence.Tag && !CacheDirTag.IsCacheDir(p.Path))
        {
            return "no longer a tagged cache";
        }
        return JunkTree.Newest(p.Path) switch
        {
            (false, _) => JunkEvidence.ScanStopped,
            (true, { } t) when t > now || now - t < Settle => "modified in the last minute",
            _ => JunkTree.IsFile(p.Path) ? JunkKinds.HeldReason(p.Path) : null,
        };
    }

    /// <summary>Removes one entry; a symlink inside a folder goes, never what it points to.</summary>
    private static void Discard(string path, Action<string>? trash)
    {
        if (trash is not null)
        {
            trash(path);
        }
        else if (JunkTree.IsDirectory(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    private static void Remove(JunkPlanned p, Action<string>? trash)
    {
        if (!KeepTop.Contains(p.Kind, StringComparer.Ordinal))
        {
            Discard(p.Path, trash);
            return;
        }
        foreach (var e in Directory.EnumerateFileSystemEntries(p.Path))
        {
            if (!string.Equals(Path.GetFileName(e), CacheDirTag.FileName, StringComparison.Ordinal))
            {
                Discard(e, trash);
            }
        }
    }

    /// <summary>
    /// Re-checks and removes every planned item. Archives go through the store's retention with
    /// their rows, never one file at a time and never to the trash.
    /// </summary>
    private static void Apply(JunkClearOptions o, IReadOnlyList<JunkPlanned> items)
    {
        var trash = o.Filter.Trash ? o.Trash : null;
        foreach (var p in items.Where(p => p.Clears && p.Kind is not "archive"))
        {
            if (Recheck(p, o.Protected, o.View.Now) is var (why, failed))
            {
                p.Skip(why, failed);
                continue;
            }
            // git unregisters a worktree, so there is no folder to move to the trash.
            if (p.Kind is "stale-worktrees" && trash is not null)
            {
                p.Skip("git removes a worktree: not moved to trash", true);
                continue;
            }
            try
            {
                p.Note = RemoveOne(o, p, trash);
            }
            catch (Exception e)
                when (e
                        is IOException
                            or UnauthorizedAccessException
                            or PlatformNotSupportedException
                )
            {
                p.Skip($"failed, kept: {e.Message}", true);
            }
        }
        var archives = items.Where(p => p.Clears && p.Kind is "archive").ToList();
        if (archives.Count == 0)
        {
            return;
        }
        var error = Retention(o);
        foreach (var p in archives)
        {
            if (error is null)
            {
                p.Note = "removed";
            }
            else
            {
                p.Skip($"failed, kept: {error}", true);
            }
        }
    }

    /// <summary>Removes one checked item and says how; a failure throws and the item stays.</summary>
    private static string RemoveOne(JunkClearOptions o, JunkPlanned p, Action<string>? trash)
    {
        if (p.Kind is not "stale-worktrees")
        {
            Remove(p, trash);
            return trash is null ? "removed" : "moved to trash";
        }
        return o.RemoveWorktree is { } git
            ? git(p.Path)
            : throw new IOException("no worktree remover");
    }

    /// <summary>The store's retention over the archives and their rows; the error when it did not run.</summary>
    private static string? Retention(JunkClearOptions o)
    {
        if (o.Store is null)
        {
            return "the store could not be opened";
        }
        try
        {
            var days = (int)Math.Min(RetentionDays(o.Config, o.Filter), int.MaxValue);
            o.Store.RunRetention(
                days,
                (int)Math.Min(o.Config.Core.RetainHookBodiesDays, int.MaxValue)
            );
            return null;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or DbException)
        {
            return e.Message;
        }
    }

    private static JunkRow RowOf(JunkPlanned p) =>
        new()
        {
            Agent = p.Agent,
            Kind = p.Kind,
            Class = "",
            Path = p.Path,
            SizeBytes = p.Bytes,
            LastUsed = p.LastUsed,
            Reason = p.Reason,
            WillClear = p.Planned,
            SkipReason = p.Clears ? null : p.Note,
            Result = p.Clears ? p.Note : "",
            Stamped = true,
        };

    /// <summary>Each kind's items as <c>list</c> prints them with planned and freed bytes, a dry run's removal preview, then the total.</summary>
    public static string Text(JunkCleared c, JunkView view)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(view);
        var sb = new StringBuilder();
        foreach (var g in JunkGroup.Of(c.Items.Select(RowOf), view))
        {
            sb.Append(g.Lines(view, "  "));
            var of = c
                .Items.Where(p =>
                    string.Equals(p.Agent, g.Agent, StringComparison.Ordinal)
                    && string.Equals(p.Kind, g.Kind, StringComparison.Ordinal)
                )
                .ToList();
            var planned = view.Size(of.Where(p => p.Planned).Sum(p => p.Bytes));
            var what = c.Yes
                ? $"freed {view.Size(of.Where(p => p.Clears).Sum(p => p.Bytes))} of {planned} planned"
                : $"{planned} planned";
            sb.Append(g.Agent).Append(' ').Append(g.Kind).Append(": ").Append(what).Append('\n');
        }
        var n = c.Items.Count(p => p.Planned);
        if (!c.Yes && n > 0)
        {
            var preview = new Preview();
            foreach (var p in c.Items.Where(p => p.Planned))
            {
                preview.Remove(p.Path, p.Bytes, p.Files);
            }
            sb.Append(preview.Render(PreviewMode.Stat));
        }
        sb.Append(
            (c.Yes, n) switch
            {
                (_, 0) => "Nothing to clear\n",
                (false, _) =>
                    $"dry run: {n} items, {view.Size(c.PlannedBytes)} to free, nothing changed; rerun with --yes\n",
                _ => $"Freed {view.Size(c.FreedBytes)} of {view.Size(c.PlannedBytes)} planned\n",
            }
        );
        return sb.ToString();
    }

    /// <summary><c>--json</c>: <c>{yes, items[], planned_bytes, freed_bytes}</c>, no trailing newline.</summary>
    public static string Json(JunkCleared c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return JunkList.Write(w =>
        {
            w.WriteStartObject();
            w.WriteBoolean("yes", c.Yes);
            w.WriteStartArray("items");
            foreach (var p in c.Items)
            {
                w.WriteStartObject();
                w.WriteString("agent", p.Agent);
                w.WriteString("kind", p.Kind);
                w.WriteString("path", p.Path);
                w.WriteNumber("bytes", p.Bytes);
                w.WriteNumber("files", p.Files);
                JunkList.WriteTime(w, "last_used", p.LastUsed);
                w.WriteString("reason", p.Reason);
                w.WriteBoolean("planned", p.Planned);
                w.WriteString("action", p.Action);
                w.WriteString("note", p.Note);
                w.WriteBoolean("failed", p.Failed);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteNumber("planned_bytes", c.PlannedBytes);
            w.WriteNumber("freed_bytes", c.FreedBytes);
            w.WriteEndObject();
        });
    }
}
