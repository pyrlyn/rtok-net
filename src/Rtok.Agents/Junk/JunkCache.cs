using Pyrlyn.FsKit;

namespace Rtok.Agents.Junk;

/// <summary>
/// The <c>cache</c> kind: a path is cache only with evidence (a §22 row, a valid
/// <c>CACHEDIR.TAG</c>, or rtok's say-so for what rtok writes). A tagged directory is judged by
/// T152's rules: idle for the window, and never the cache of the worktree the command runs from.
/// </summary>
public static class JunkCache
{
    /// <summary>
    /// Why a cache stays, or null when it goes (T152). <paramref name="current"/>: it sits in the
    /// worktree this command runs from; <paramref name="explicitPath"/>: its worktree was named.
    /// A missing age keeps it, since a deletion cannot be undone.
    /// </summary>
    public static string? KeptBecause(
        DateTime? newest,
        bool incomplete,
        bool current,
        bool explicitPath,
        TimeSpan idle,
        DateTime now
    )
    {
        if (current && !explicitPath)
        {
            return "the worktree this command runs from; name its path to clean it";
        }
        return newest switch
        {
            { } m when now - m < idle => "modified within the idle window",
            null => JunkEvidence.AgeUnknown,
            _ when incomplete => JunkEvidence.AgeUnknown,
            _ => null,
        };
    }

    /// <summary>An item of <paramref name="kind"/> sized within the run's limit.</summary>
    internal static JunkItem Make(
        string kind,
        string path,
        string evidence,
        string? kept,
        JunkContext cx,
        string cls = "safe"
    ) => new(kind, cls, path, JunkTree.Bytes(path, cx.Limit), evidence, kept);

    /// <summary>T152's idle answer for one directory, or <see cref="JunkEvidence.ScanStopped"/> when the walk did not finish.</summary>
    internal static string? IdleKeep(string path, bool current, JunkContext cx, TimeSpan idle)
    {
        var ages = FsTree.Ages(path, cx.Limit);
        return ages.TimedOut
            ? JunkEvidence.ScanStopped
            : KeptBecause(ages.Newest, ages.Incomplete, current, false, idle, cx.Now);
    }

    /// <summary>
    /// The cache items under one agent: its <paramref name="owned"/> directories plus every tagged
    /// directory below <paramref name="roots"/>. A link out of the folder is never followed.
    /// </summary>
    internal static List<JunkItem> Items(
        IEnumerable<JunkOwned> owned,
        IEnumerable<string> roots,
        JunkContext cx
    )
    {
        ArgumentNullException.ThrowIfNull(owned);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(cx);
        var items = new List<JunkItem>();
        foreach (var o in owned)
        {
            if (!JunkTree.IsDirectory(o.Path))
            {
                continue;
            }
            var kept = o.IdleRule ? IdleKeep(o.Path, false, cx, cx.Idle) : null;
            items.Add(Make("cache", o.Path, o.Evidence, kept, cx));
        }
        foreach (var root in roots.Where(Directory.Exists))
        {
            items.AddRange(Tagged("cache", root, cx));
        }
        return DropNested(items);
    }

    /// <summary>The tagged caches under <paramref name="root"/> as items of <paramref name="kind"/>.</summary>
    internal static IEnumerable<JunkItem> Tagged(string kind, string root, JunkContext cx)
    {
        var ages = FsTree.Ages(root, cx.Limit);
        var current = JunkTree.Under(cx.Cwd, JunkTree.Real(root));
        return ages.Caches.Select(c =>
            Make(
                kind,
                c.Path,
                JunkEvidence.Tag,
                ages.TimedOut
                    ? JunkEvidence.ScanStopped
                    : KeptBecause(c.Newest, c.Incomplete, current, false, cx.Idle, cx.Now),
                cx
            )
        );
    }

    /// <summary>
    /// One row per path (the first evidence wins), and none inside a counted path: clearing the
    /// outer one already frees it. Sorted by path.
    /// </summary>
    internal static List<JunkItem> DropNested(IEnumerable<JunkItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var unique = items
            .Select((item, order) => (item, order))
            .OrderBy(x => x.item.Path, StringComparer.Ordinal)
            .ThenBy(x => x.order)
            .Select(x => x.item)
            .DistinctBy(i => i.Path, StringComparer.Ordinal)
            .ToList();
        var counted = unique.Where(i => i.Counted).Select(i => i.Path).ToList();
        return
        [
            .. unique.Where(i =>
                !counted.Exists(c =>
                    !string.Equals(c, i.Path, StringComparison.Ordinal) && JunkTree.Under(i.Path, c)
                )
            ),
        ];
    }
}
