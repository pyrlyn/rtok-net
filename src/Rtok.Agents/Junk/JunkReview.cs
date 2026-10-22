using System.Diagnostics;
using Pyrlyn.Ignore;
using Rtok.Agent.Sdk;
using Rtok.Config;
using Rtok.Hook.Contracts;

namespace Rtok.Agents.Junk;

/// <summary>
/// The <c>logs</c> (review), <c>backups</c> (rtok's own <c>_backup</c> generations, review),
/// <c>snapshots</c> (Gemini CLI's restore points, never) and <c>crash-dumps</c> kinds, the
/// <c>[agents.junk] extra</c> paths and the <c>exclude</c> globs. Nothing here deletes.
/// </summary>
public static class JunkReview
{
    /// <summary>Why an excluded item stays.</summary>
    public const string Excluded = "excluded by [agents.junk] exclude";

    private const string Snapshot = "snapshot: never cleared; Gemini CLI restores it with /restore";

    private static readonly string[] ReportExtensions = ["ips", "crash", "dmp", "diag"];

    private static List<JunkItem> Classed(IEnumerable<JunkItem> items, string cls) =>
        [.. items.Select(i => i with { Class = cls })];

    /// <summary>Entries of the §22 log folders (or a single log file) older than <c>keep_logs_days</c>.</summary>
    internal static List<JunkItem> LogItems(
        IEnumerable<string> dirs,
        AgentsJunkSection junk,
        JunkContext cx
    )
    {
        ArgumentNullException.ThrowIfNull(junk);
        return Classed(
            JunkKinds.AgedItems(
                "logs",
                dirs,
                JunkEvidence.Section22,
                TimeSpan.FromDays(junk.KeepLogsDays),
                cx
            ),
            "review"
        );
    }

    /// <summary>rtok's copies in the <c>_backup</c> folder of each of <paramref name="dirs"/> past <c>setup.backup_files</c>; the newest stays.</summary>
    internal static List<JunkItem> BackupItems(IEnumerable<string> dirs, uint keep, JunkContext cx)
    {
        ArgumentNullException.ThrowIfNull(dirs);
        var cap = (int)Math.Min(keep, int.MaxValue);
        return
        [
            .. dirs.SelectMany(d => Backups.Stale(Path.Combine(d, Backups.Dir), cap))
                .Select(p =>
                    JunkCache.Make("backups", p, JunkEvidence.RtokOwn, null, cx, "review")
                ),
        ];
    }

    /// <summary>Gemini CLI's restore points (research.md §22.1): class never, listed for their size with the host's own way back.</summary>
    internal static List<JunkItem> SnapshotItems(JunkRoots roots, JunkContext cx)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var gemini = roots.Resolve("{home}/.gemini");
        var dirs = JunkTree
            .Children(Path.Combine(gemini, "history"))
            .Concat(
                JunkTree
                    .Children(Path.Combine(gemini, "tmp"))
                    .Select(p => Path.Combine(p, "checkpoints"))
            );
        return
        [
            .. dirs.Where(JunkTree.IsDirectory)
                .Select(d =>
                    JunkCache.Make("snapshots", d, JunkEvidence.Section22, Snapshot, cx, "never")
                ),
        ];
    }

    /// <summary>
    /// The entries of an <c>extra</c> crash folder: only sized and removed, never read. One older
    /// than <c>crash_dump_min_age_days</c> is safe, a younger one (or of unknown age) review.
    /// </summary>
    internal static List<JunkItem> CrashDumpItems(
        IEnumerable<string> dirs,
        AgentsJunkSection junk,
        JunkContext cx
    )
    {
        ArgumentNullException.ThrowIfNull(junk);
        ArgumentNullException.ThrowIfNull(cx);
        var old = TimeSpan.FromDays(junk.CrashDumpMinAgeDays);
        return
        [
            .. JunkKinds
                .AgedItems("crash-dumps", dirs, JunkEvidence.Extra, TimeSpan.Zero, cx)
                .Select(i =>
                    i with
                    {
                        Class =
                            JunkTree.Newest(i.Path) is (true, { } t) && cx.Now - t > old
                                ? "safe"
                                : "review",
                    }
                ),
        ];
    }

    /// <summary>
    /// macOS <c>~/Library/Logs/DiagnosticReports</c> files named for one of <paramref name="names"/>
    /// (<c>Cursor-2026-…ips</c>). §22 has no row, so each is listed and kept.
    /// </summary>
    internal static List<JunkItem> DiagnosticReports(
        IReadOnlyCollection<string> names,
        JunkRoots roots,
        JunkContext cx
    )
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(roots);
        bool Named(string file)
        {
            file = file.ToLowerInvariant();
            var dot = file.LastIndexOf('.');
            return dot >= 0
                && ReportExtensions.Contains(file[(dot + 1)..])
                && names.Any(n =>
                    file.Length > n.Length
                    && file.StartsWith(n, StringComparison.Ordinal)
                    && "-_. ".Contains(file[n.Length], StringComparison.Ordinal)
                );
        }
        return
        [
            .. JunkTree
                .Children(roots.Resolve("{home}/Library/Logs/DiagnosticReports"))
                .Where(p => JunkTree.IsFile(p) && Named(Path.GetFileName(p)))
                .Select(p =>
                    JunkCache.Make(
                        "crash-dumps",
                        p,
                        JunkEvidence.None,
                        JunkEvidence.NotDocumented,
                        cx,
                        "review"
                    )
                ),
        ];
    }

    /// <summary>
    /// The <c>[agents.junk] extra</c> entries of <paramref name="agent"/>: a cache folder joins the
    /// owned caches; a temp, logs or crash-dumps folder gives its entries by that kind's age rule.
    /// A relative path or an unknown kind, which <c>config validate</c> refuses, is ignored here.
    /// </summary>
    internal static (List<JunkOwned> Owned, List<JunkItem> Items) ExtraItems(
        AgentsJunkSection junk,
        string agent,
        string rtokHome,
        string home,
        JunkContext cx
    )
    {
        ArgumentNullException.ThrowIfNull(junk);
        var owned = new List<JunkOwned>();
        var items = new List<JunkItem>();
        foreach (
            var e in junk.Extra.Where(e => string.Equals(e.Host, agent, StringComparison.Ordinal))
        )
        {
            var path = RtokHome.Expand(e.Path, rtokHome, home);
            if (!Path.IsPathRooted(path))
            {
                continue;
            }
            string[] one = [path];
            switch (e.Kind)
            {
                case "cache":
                    owned.Add(new JunkOwned(path, JunkEvidence.Extra, false));
                    break;
                case "temp":
                    items.AddRange(
                        JunkKinds.AgedItems(
                            "temp",
                            one,
                            JunkEvidence.Extra,
                            TimeSpan.FromHours(junk.TempMinAgeHours),
                            cx
                        )
                    );
                    break;
                case "logs":
                    items.AddRange(
                        Classed(
                            JunkKinds.AgedItems(
                                "logs",
                                one,
                                JunkEvidence.Extra,
                                TimeSpan.FromDays(junk.KeepLogsDays),
                                cx
                            ),
                            "review"
                        )
                    );
                    break;
                case "crash-dumps":
                    items.AddRange(CrashDumpItems(one, junk, cx));
                    break;
            }
        }
        return (owned, items);
    }

    /// <summary>
    /// Keeps every counted item an exclude glob matches, and every folder holding a match: a cache
    /// emptied whole would take the excluded file with it. A glob that does not compile keeps
    /// every item, so protection that cannot be checked is never dropped.
    /// </summary>
    internal static List<JunkItem> Exclude(
        IEnumerable<JunkItem> items,
        IEnumerable<string> globs,
        string rtokHome,
        string home,
        JunkContext cx
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(globs);
        ArgumentNullException.ThrowIfNull(cx);
        var list = globs.ToList();
        if (list.Count == 0)
        {
            return [.. items];
        }
        var (patterns, error) = Compile(list, rtokHome, home);
        return
        [
            .. items.Select(i =>
            {
                if (!i.Counted)
                {
                    return i;
                }
                if (error is not null)
                {
                    return i with
                    {
                        Kept = $"invalid [agents.junk] exclude, nothing cleared: {error}",
                    };
                }
                var hit =
                    Matches(patterns, i.Path)
                    || (JunkTree.IsDirectory(i.Path) && HoldsMatch(i.Path, patterns, cx.Limit));
                return hit ? i with { Kept = Excluded } : i;
            }),
        ];
    }

    private static (List<IgnorePattern> Patterns, string? Error) Compile(
        List<string> globs,
        string rtokHome,
        string home
    )
    {
        var patterns = new List<IgnorePattern>();
        foreach (var g in globs)
        {
            var path = RtokHome.Expand(g, rtokHome, home);
            if (!Path.IsPathRooted(path))
            {
                return ([], $"{g}: not an absolute path");
            }
            try
            {
                if (IgnorePattern.Parse("/" + Slashed(path)) is not { Negated: false } p)
                {
                    return ([], $"{g}: not a glob");
                }
                patterns.Add(p);
            }
            catch (ArgumentException e)
            {
                return ([], $"{g}: {e.Message}");
            }
        }
        return (patterns, null);
    }

    /// <summary>An absolute path as the root-relative, <c>/</c>-separated form the patterns match.</summary>
    private static string Slashed(string path) => path.Replace('\\', '/').TrimStart('/');

    private static bool Matches(List<IgnorePattern> patterns, string path)
    {
        var rel = Slashed(path);
        var dir = Directory.Exists(path);
        return patterns.Exists(p => p.IsMatch(rel, dir));
    }

    /// <summary>Whether anything under <paramref name="dir"/> matches; a walk cut by the limit answers yes.</summary>
    private static bool HoldsMatch(string dir, List<IgnorePattern> patterns, TimeSpan limit)
    {
        var clock = Stopwatch.StartNew();
        var stack = new Stack<string>();
        stack.Push(dir);
        while (stack.Count > 0)
        {
            if (clock.Elapsed >= limit)
            {
                return true;
            }
            foreach (var p in JunkTree.Children(stack.Pop()))
            {
                if (Matches(patterns, p))
                {
                    return true;
                }
                if (JunkTree.IsDirectory(p))
                {
                    stack.Push(p);
                }
            }
        }
        return false;
    }
}
