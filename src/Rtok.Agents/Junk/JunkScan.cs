using System.Globalization;
using Pyrlyn.FsKit;
using Rtok.Config;

namespace Rtok.Agents.Junk;

/// <summary>Builds the <see cref="JunkReport"/>: read-only.</summary>
public static class JunkScan
{
    /// <summary>The kinds in the order the tables list them.</summary>
    public static IReadOnlyList<string> Kinds { get; } =
    [
        "cache",
        "temp",
        "logs",
        "build",
        "deps",
        "locks",
        "backups",
        "swap",
        "snapshots",
        "index",
        "crash-dumps",
        "sessions",
        "stale-worktrees",
    ];

    private static readonly string[] OwnKinds = ["log", "archive"];

    /// <summary>Every kind's class, as the catalog gives it: the class gate of <c>clear</c>.</summary>
    public static string ClassOf(string kind) =>
        kind switch
        {
            "cache"
            or "temp"
            or "build"
            or "locks"
            or "swap"
            or "crash-dumps"
            or "log"
            or "archive" => "safe",
            "logs" or "deps" or "backups" or "index" or "stale-worktrees" => "review",
            "sessions" => "explicit",
            _ => "never",
        };

    /// <summary>
    /// <c>rtok.log.&lt;N&gt;</c> siblings beyond <c>[log] files</c> and the archives past
    /// <paramref name="days"/>. An unreadable path is skipped, not fatal.
    /// </summary>
    internal static List<JunkOutcome> Own(RtokConfig config, IJunkStore? store, int days)
    {
        ArgumentNullException.ThrowIfNull(config);
        var outcomes = new List<JunkOutcome>();
        var log = config.Log.Path;
        var dir = Path.GetDirectoryName(log);
        var name = Path.GetFileName(log);
        if (dir is not null)
        {
            foreach (var p in JunkTree.Children(dir).Order(StringComparer.Ordinal))
            {
                var file = Path.GetFileName(p);
                if (
                    file.StartsWith(name + ".", StringComparison.Ordinal)
                    && uint.TryParse(
                        file[(name.Length + 1)..],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var n
                    )
                    && n > config.Log.Files
                    && JunkTree.IsFile(p)
                )
                {
                    outcomes.Add(
                        new(
                            "log",
                            p,
                            new FileInfo(p).Length,
                            $"past `[log] files` = {config.Log.Files}"
                        )
                    );
                }
            }
        }
        if (store is not null)
        {
            try
            {
                foreach (var p in store.ArchivesPendingRetention(days))
                {
                    var size = File.Exists(p) ? new FileInfo(p).Length : 0;
                    outcomes.Add(
                        new("archive", p, size, $"past `core.retain_calls_days` = {days}")
                    );
                }
            }
            catch (Exception e) when (e is IOException or InvalidOperationException) { }
        }
        return outcomes;
    }

    /// <summary>The report: rtok's own row, then every installed host (all with <see cref="JunkOptions.All"/>).</summary>
    public static JunkReport Build(JunkOptions o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var own = Own(
            o.Config,
            o.Store,
            (int)Math.Min(o.Config.Core.RetainCallsDays, int.MaxValue)
        );
        var rtok = RtokRow(o, own);
        var agents = new List<JunkAgent> { rtok };
        var (hosts, sized) = HostRows(o);
        agents.AddRange(hosts);
        foreach (var f in rtok.Folders)
        {
            sized[f.Path] = f.SizeBytes;
        }
        var totalBytes = JunkTree.TopLevelBytes([.. sized.Select(kv => (kv.Key, kv.Value))]);
        var logArchive = rtok
            .Kinds.Where(k => !Kinds.Contains(k.Kind, StringComparer.Ordinal))
            .Sum(k => k.SizeBytes);
        // Agents by space freed, largest first; stable, so rtok keeps the lead on a tie.
        var sorted = agents.OrderByDescending(a => a.FreedDefaultBytes).ToList();
        return new JunkReport(
            sorted,
            own,
            totalBytes,
            logArchive + FreedAll(agents, false),
            logArchive + FreedAll(agents, true)
        );
    }

    /// <summary>What <c>clear</c> frees over every agent, each path once and a nested one folded into its parent.</summary>
    private static long FreedAll(List<JunkAgent> agents, bool review)
    {
        var byPath = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (
            var i in agents
                .SelectMany(a => a.Items)
                .Where(i => i.Counted && (i.Class is "safe" || (review && i.Class is "review")))
        )
        {
            byPath[i.Path] = i.Bytes;
        }
        return JunkTree.TopLevelBytes([.. byPath.Select(kv => (kv.Key, kv.Value))]);
    }

    /// <summary>rtok's own row: its log and archive files, its cache, the language servers' state and the extra paths.</summary>
    private static JunkAgent RtokRow(JunkOptions o, List<JunkOutcome> own)
    {
        var cx = o.Context;
        var junk = o.Config.Agents.Junk;
        List<JunkKindRow> kinds =
        [
            .. OwnKinds
                .Select(k =>
                    (
                        k,
                        rows: own.Where(x => string.Equals(x.Kind, k, StringComparison.Ordinal))
                            .ToList()
                    )
                )
                .Where(x => x.rows.Count > 0)
                .Select(x => new JunkKindRow(x.k, "safe", x.rows.Count, x.rows.Sum(r => r.Bytes))),
        ];
        var rtokCache = o.Roots.Resolve("{rtok_cache}");
        var folders = RtokFolders(o.Config, rtokCache, cx);
        var (extraOwned, extra) = JunkReview.ExtraItems(junk, "rtok", o.RtokHome, o.Roots.Home, cx);
        List<JunkOwned> owned =
        [
            new(rtokCache, JunkEvidence.RtokOwn, false),
            .. LspDirs(o.LspStates, "cache", "pub-cache"),
            .. JunkKinds.StagingCaches(o.Roots.Resolve("{claude}")),
            .. extraOwned,
        ];
        var cache = JunkCache.Items(owned, JunkTree.Outermost(folders.Select(f => f.Path)), cx);
        cache.AddRange(extra);
        // The language servers' data and state hold their index: review, kept like a cache.
        cache.AddRange(
            JunkCache
                .Items(LspDirs(o.LspStates, "data", "state"), [], cx)
                .Select(i => i with { Kind = "index", Class = "review" })
        );
        cache.AddRange(o.StaleWorktrees.Where(s => s.Agent is "rtok").Select(s => s.Item));
        var items = JunkCache.DropNested(
            JunkReview.Exclude(cache, junk.Exclude, o.RtokHome, o.Roots.Home, cx)
        );
        kinds.AddRange(KindRows(items));
        return new(
            "rtok",
            false,
            true,
            folders,
            folders.Sum(f => f.SizeBytes),
            kinds,
            items,
            [],
            Freed(kinds, false),
            Freed(kinds, true)
        );
    }

    private static IEnumerable<JunkOwned> LspDirs(
        IEnumerable<string> states,
        params string[] subdirs
    ) =>
        states.SelectMany(s =>
            subdirs.Select(d => new JunkOwned(Path.Combine(s, d), JunkEvidence.RtokOwn, true))
        );

    /// <summary>rtok's log, archive and cache directories, one nested in another folded into the outer.</summary>
    private static List<JunkFolder> RtokFolders(RtokConfig config, string cache, JunkContext cx)
    {
        var dirs = new[] { Path.GetDirectoryName(config.Log.Path), config.Core.ArchiveDir, cache }
            .OfType<string>()
            .Where(Directory.Exists);
        return
        [
            .. JunkTree
                .Outermost(dirs)
                .Select(d => new JunkFolder(
                    d,
                    string.Equals(d, cache, StringComparison.Ordinal) ? "cache" : "data",
                    JunkTree.Bytes(d, cx.Limit),
                    true,
                    [],
                    null,
                    d
                )),
        ];
    }

    /// <summary>One row per kind and class of the counted items.</summary>
    internal static List<JunkKindRow> KindRows(IReadOnlyList<JunkItem> items)
    {
        var rows = new List<JunkKindRow>();
        foreach (var kind in Kinds)
        {
            foreach (var cls in new[] { "safe", "review", "explicit" })
            {
                var counted = items
                    .Where(i =>
                        string.Equals(i.Kind, kind, StringComparison.Ordinal)
                        && string.Equals(i.Class, cls, StringComparison.Ordinal)
                        && i.Counted
                    )
                    .ToList();
                if (counted.Count > 0)
                {
                    rows.Add(new(kind, cls, counted.Count, counted.Sum(i => i.Bytes)));
                }
            }
        }
        return rows;
    }

    /// <summary>What <c>clear</c> frees: the safe rows, plus the review ones with <paramref name="review"/>.</summary>
    private static long Freed(IEnumerable<JunkKindRow> rows, bool review) =>
        rows.Where(k => k.Class is "safe" || (review && k.Class is "review")).Sum(k => k.SizeBytes);

    /// <summary>The folders a host's own config files sit in, under the home only, never the home itself.</summary>
    private static List<string> MarkerDirs(JunkHost host, JunkRoots roots) =>
        [
            .. host
                .Markers.Select(Path.GetDirectoryName)
                .OfType<string>()
                .Where(d =>
                    !string.Equals(d, roots.Home, StringComparison.Ordinal)
                    && JunkTree.Under(d, roots.Home)
                    && JunkTree.IsDirectory(d)
                )
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

    private sealed record Found(string Path, JunkRole Role, bool Documented, bool PerCommit);

    /// <summary>The folders of one host that exist: the §22 map, then the folders its config files sit in.</summary>
    private static List<Found> HostFolders(JunkHost host, JunkRoots roots)
    {
        var found = new List<Found>();
        foreach (var s in JunkMap.Specs(host.Id))
        {
            var path = roots.Resolve(s.Path);
            if (
                !found.Exists(f => string.Equals(f.Path, path, StringComparison.Ordinal))
                && JunkTree.Exists(path)
            )
            {
                found.Add(new(path, s.Role, s.Documented, s.PerCommit));
            }
        }
        foreach (var d in MarkerDirs(host, roots).OrderBy(d => d.Count(c => c is '/' or '\\')))
        {
            if (!found.Exists(f => JunkTree.Under(d, f.Path)))
            {
                found.Add(new(d, JunkRole.Data, false, false));
            }
        }
        return [.. found.OrderBy(f => f.Path, StringComparer.Ordinal)];
    }

    /// <summary>What one host's folders give the item scan.</summary>
    private sealed record Scanned(
        List<JunkFolder> Folders,
        List<JunkOwned> Owned,
        List<string> Temp,
        List<string> Logs
    );

    private static (List<JunkAgent> Rows, Dictionary<string, long> Sized) HostRows(JunkOptions o)
    {
        var sized = new Dictionary<string, TreeUsage>(StringComparer.Ordinal);
        var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var built =
            new List<(
                JunkHost Host,
                List<JunkFolder> Folders,
                List<JunkItem> Items,
                List<string> Notes
            )>();
        foreach (var host in o.Hosts.Where(h => h.Installed || o.All))
        {
            var scanned = ScanFolders(host, o, sized, owners);
            var (items, notes) = HostItems(host, o, scanned);
            built.Add((host, scanned.Folders, items, notes));
        }
        var rows = built
            .Select(b =>
            {
                List<JunkFolder> folders =
                [
                    .. b.Folders.Select(f =>
                        f with
                        {
                            SharedWith =
                            [
                                .. owners[f.Key]
                                    .Where(n =>
                                        !string.Equals(n, b.Host.Id, StringComparison.Ordinal)
                                    ),
                            ],
                        }
                    ),
                ];
                var kinds = KindRows(b.Items);
                return new JunkAgent(
                    b.Host.Id,
                    true,
                    b.Host.Installed,
                    folders,
                    JunkTree.TopLevelBytes([.. folders.Select(f => (f.Key, f.SizeBytes))]),
                    kinds,
                    b.Items,
                    b.Notes,
                    Freed(kinds, false),
                    Freed(kinds, true)
                );
            })
            .ToList();
        return (
            rows,
            sized.ToDictionary(kv => kv.Key, kv => kv.Value.Bytes, StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// One host's folders, each real directory sized once over all hosts (<paramref name="sized"/>)
    /// with the hosts that list it (<paramref name="owners"/>), and the caches, temp and log folders in them.
    /// </summary>
    private static Scanned ScanFolders(
        JunkHost host,
        JunkOptions o,
        Dictionary<string, TreeUsage> sized,
        Dictionary<string, List<string>> owners
    )
    {
        var scanned = new Scanned([], [], [], []);
        foreach (var f in HostFolders(host, o.Roots))
        {
            var target = JunkTree.IsSymlink(f.Path) ? JunkTree.Real(f.Path) : null;
            var key = target ?? f.Path;
            if (f.PerCommit)
            {
                scanned.Owned.AddRange(JunkKinds.StaleCodeCaches(f.Path));
            }
            else if (f.Role == JunkRole.Cache && f.Documented)
            {
                scanned.Owned.Add(new(f.Path, JunkEvidence.Section22, false));
            }
            if (f.Role == JunkRole.Temp && f.Documented)
            {
                scanned.Temp.Add(f.Path);
            }
            if (f.Role == JunkRole.Logs && f.Documented)
            {
                scanned.Logs.Add(f.Path);
            }
            if (!sized.TryGetValue(key, out var usage))
            {
                usage = FsTree.Usage(key, o.Context.Limit);
                sized[key] = usage;
            }
            if (!owners.TryGetValue(key, out var who))
            {
                owners[key] = who = [];
            }
            who.Add(host.Id);
            scanned.Folders.Add(
                new(
                    f.Path,
                    f.Role.ToString().ToLowerInvariant(),
                    usage.Bytes,
                    f.Documented,
                    [],
                    FolderNote(target, usage, o.Context.Limit),
                    key
                )
            );
        }
        return scanned;
    }

    /// <summary>Why a folder's size may be low, or that it is a symlink.</summary>
    private static string? FolderNote(string? target, TreeUsage usage, TimeSpan limit)
    {
        var notes = new List<string>();
        if (target is not null)
        {
            notes.Add($"symlink to {target}");
        }
        if (usage.Denied)
        {
            notes.Add("permission denied: size is a lower bound");
        }
        if (usage.TimedOut)
        {
            notes.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"scan stopped after {(int)limit.TotalSeconds}s: size is a lower bound"
                )
            );
        }
        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>One host's items of every kind, excludes applied and nested ones dropped, with the notes under its kinds.</summary>
    private static (List<JunkItem> Items, List<string> Notes) HostItems(
        JunkHost host,
        JunkOptions o,
        Scanned scanned
    )
    {
        var cx = o.Context;
        var junk = o.Config.Agents.Junk;
        var (extraOwned, extra) = JunkReview.ExtraItems(
            junk,
            host.Id,
            o.RtokHome,
            o.Roots.Home,
            cx
        );
        var tagRoots = JunkTree.Outermost(scanned.Folders.Select(f => f.Key));
        var items = JunkCache.Items([.. scanned.Owned, .. extraOwned], tagRoots, cx);
        items.AddRange(extra);
        items.AddRange(
            JunkKinds.TempItems(scanned.Temp, TimeSpan.FromHours(junk.TempMinAgeHours), cx)
        );
        items.AddRange(JunkReview.LogItems(scanned.Logs, junk, cx));
        items.AddRange(JunkKinds.FoundItems(tagRoots, cx));
        items.AddRange(
            JunkReview.BackupItems(MarkerDirs(host, o.Roots), o.Config.Setup.BackupFiles, cx)
        );
        if (host.Id is "gemini")
        {
            items.AddRange(JunkReview.SnapshotItems(o.Roots, cx));
        }
        items.AddRange(JunkReview.DiagnosticReports([.. host.ProcessNames], o.Roots, cx));
        var notes = new List<string>();
        if (host.Id is "claude")
        {
            var old = JunkSessions.SessionItems(o.Roots, junk.StaleSessionDays, cx);
            if (old.Count > 0)
            {
                notes.Add(JunkSessions.Note(junk.StaleSessionDays));
            }
            items.AddRange(old);
        }
        if (scanned.Folders.Count > 0 && JunkSessions.Retention(o.Roots, host.Id) is { } retention)
        {
            notes.Add(retention);
        }
        items.AddRange(
            JunkKinds.BuildItems(
                o.Worktrees.Where(w => string.Equals(w.Host, host.Id, StringComparison.Ordinal))
                    .Select(w => w.Path),
                cx
            )
        );
        items.AddRange(
            o.StaleWorktrees.Where(s => string.Equals(s.Agent, host.Id, StringComparison.Ordinal))
                .Select(s => s.Item)
        );
        return (
            JunkCache.DropNested(
                JunkReview.Exclude(items, junk.Exclude, o.RtokHome, o.Roots.Home, cx)
            ),
            notes
        );
    }
}
