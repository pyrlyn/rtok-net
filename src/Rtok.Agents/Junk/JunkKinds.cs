using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Pyrlyn.FsKit;
using Rtok.Agent.Sdk;

namespace Rtok.Agents.Junk;

/// <summary>
/// The <c>temp</c>, <c>build</c>, <c>deps</c>, <c>locks</c>, <c>swap</c>, <c>index</c> and
/// <c>backups</c> kinds, and rtok's plugin staging caches. A lock or swap file has no D36
/// evidence (§22 names none), so it is listed read-only with the reason a live owner gives first.
/// </summary>
public static class JunkKinds
{
    /// <summary>Lock files that are package-manager state and never junk.</summary>
    public static IReadOnlyList<string> PackageLocks { get; } =
    [
        "Cargo.lock",
        "yarn.lock",
        "bun.lock",
        "poetry.lock",
        "uv.lock",
        "pdm.lock",
        "Pipfile.lock",
        "Gemfile.lock",
        "composer.lock",
        "flake.lock",
        "Podfile.lock",
        "pubspec.lock",
        "mix.lock",
        "deno.lock",
    ];

    /// <summary>Claude Code's copies of rtok's plugin, under its config dir.</summary>
    internal const string PluginCache = "plugins/cache/rtok/rtok";

    private const string PluginId = "rtok@rtok";

    /// <summary>How deep a walk for leftovers goes: an agent folder is shallow, a deep tree is a checkout.</summary>
    private const int MaxDepth = 12;

    private static readonly string[] SkipDirs = [".git", "node_modules"];

    /// <summary>Build output without a tag: listed in an agent worktree, never cleared.</summary>
    private static readonly string[] BuildDirs = ["dist", ".next", "__pycache__"];

    /// <summary>Reinstallable dependency folders and the files beside one that can reinstall it.</summary>
    private static readonly (string Dir, string[] Manifests)[] Deps =
    [
        ("node_modules", ["package.json"]),
        (".venv", ["pyproject.toml", "requirements.txt", "uv.lock", "Pipfile"]),
        ("vendor", ["composer.json", "Gemfile", "go.mod"]),
        (".gradle", ["build.gradle", "build.gradle.kts", "settings.gradle"]),
        ("Pods", ["Podfile"]),
    ];

    /// <summary>The kind a leftover's name and type say, or null.</summary>
    internal static string? LockOrSwap(string name, bool file, bool dir, bool link)
    {
        if (
            (
                file
                && (
                    name.EndsWith(".swp", StringComparison.Ordinal)
                    || name.EndsWith(".swo", StringComparison.Ordinal)
                    || name.EndsWith(".crswap", StringComparison.Ordinal)
                )
            ) || (link && name.StartsWith(".#", StringComparison.Ordinal))
        )
        {
            return "swap";
        }
        if (
            !link
            && (
                (name is "LOCK" && file)
                || (
                    name.EndsWith(".lock", StringComparison.Ordinal)
                    && !PackageLocks.Contains(name, StringComparer.Ordinal)
                )
            )
        )
        {
            return "locks";
        }
        // A journal beside a database still open is live data, and nothing here can tell which is closed.
        if (
            (
                file
                && (
                    name.EndsWith("-wal", StringComparison.Ordinal)
                    || name.EndsWith("-shm", StringComparison.Ordinal)
                )
            ) || (dir && !link && name is ".rust-analyzer")
        )
        {
            return "index";
        }
        if (
            file
            && (
                name.EndsWith(".bak", StringComparison.Ordinal)
                || name.Contains(".bak-", StringComparison.Ordinal)
                || name.EndsWith('~')
            )
        )
        {
            return "backups";
        }
        return null;
    }

    /// <summary>Depth-first search under <paramref name="root"/> for what <paramref name="pick"/> names; a picked entry is not entered, a symlink never is.</summary>
    private static List<(string Path, string Kind)> Find(
        string root,
        TimeSpan limit,
        Func<string, bool> skip,
        Func<string, bool, bool, bool, string?> pick
    )
    {
        var clock = Stopwatch.StartNew();
        var found = new List<(string, string)>();
        var stack = new Stack<(string Dir, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0 && clock.Elapsed < limit)
        {
            var (dir, depth) = stack.Pop();
            foreach (var path in JunkTree.Children(dir))
            {
                var name = Path.GetFileName(path);
                var link = JunkTree.IsSymlink(path);
                var isDir = !link && Directory.Exists(path);
                var isFile = !link && File.Exists(path);
                if (pick(name, isFile, isDir, link) is { } kind)
                {
                    found.Add((path, kind));
                }
                else if (isDir && depth < MaxDepth && !SkipDirs.Contains(name) && !skip(path))
                {
                    stack.Push((path, depth + 1));
                }
            }
        }
        return found;
    }

    /// <summary>
    /// Lock, swap and backup files and the file-based index leftovers under an agent's folders.
    /// None has D36 evidence, so each is kept. rtok's own <c>_backup</c> folders are not walked.
    /// </summary>
    internal static List<JunkItem> FoundItems(IEnumerable<string> roots, JunkContext cx)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(cx);
        var items = new List<JunkItem>();
        foreach (var root in roots)
        {
            var finds = Find(
                root,
                cx.Limit,
                p => string.Equals(Path.GetFileName(p), Backups.Dir, StringComparison.Ordinal),
                LockOrSwap
            );
            foreach (var (path, kind) in finds)
            {
                var reason = kind switch
                {
                    "swap" => SwapReason(path),
                    "locks" when JunkTree.IsFile(path) => HeldReason(path),
                    _ => null,
                };
                var cls = kind is "index" or "backups" ? "review" : "safe";
                items.Add(
                    JunkCache.Make(
                        kind,
                        path,
                        JunkEvidence.None,
                        reason ?? JunkEvidence.NotDocumented,
                        cx,
                        cls
                    )
                );
            }
        }
        return items;
    }

    /// <summary>Why a file stays because a process may hold it, or null when nothing does.</summary>
    internal static string? HeldReason(string path)
    {
        try
        {
            using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            return $"cannot check whether a process holds it: {e.Message}";
        }
        catch (IOException)
        {
            return "held by a running process";
        }
    }

    /// <summary>The pid a swap file names: Vim writes it at byte 24 of its <c>b0</c> block, Emacs in the link target.</summary>
    private static int? SwapOwner(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            if (name.StartsWith(".#", StringComparison.Ordinal))
            {
                var target = new FileInfo(path).LinkTarget;
                var tail = target?[(target.LastIndexOf('.') + 1)..].Split(':')[0];
                return int.TryParse(
                    tail,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var pid
                )
                    ? pid
                    : null;
            }
            Span<byte> head = stackalloc byte[28];
            using var stream = File.OpenRead(path);
            stream.ReadExactly(head);
            return head[0] == 'b' && head[1] == '0' ? BitConverter.ToInt32(head[24..]) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? SwapReason(string path)
    {
        if (SwapOwner(path) is not { } pid)
        {
            return "owner unknown";
        }
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.HasExited ? null : $"owner process {pid} is running";
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The entries of the §22 temp dirs, kept while touched within <paramref name="minAge"/>.</summary>
    internal static List<JunkItem> TempItems(
        IEnumerable<string> dirs,
        TimeSpan minAge,
        JunkContext cx
    ) => AgedItems("temp", dirs, JunkEvidence.Section22, minAge, cx);

    /// <summary>
    /// One item per entry of each folder in <paramref name="dirs"/>, or the path itself when it
    /// is a file: kept while touched within <paramref name="minAge"/> or, for a file, while a
    /// process holds a lock on it. A symlink is never an item.
    /// </summary>
    internal static List<JunkItem> AgedItems(
        string kind,
        IEnumerable<string> dirs,
        string evidence,
        TimeSpan minAge,
        JunkContext cx
    )
    {
        ArgumentNullException.ThrowIfNull(dirs);
        ArgumentNullException.ThrowIfNull(cx);
        var items = new List<JunkItem>();
        foreach (var dir in dirs.Where(d => !JunkTree.IsSymlink(d)))
        {
            string[] entries = JunkTree.IsDirectory(dir) ? JunkTree.Children(dir) : [dir];
            foreach (var path in entries)
            {
                if (JunkTree.IsSymlink(path) || !JunkTree.Exists(path))
                {
                    continue;
                }
                string? kept;
                if (JunkTree.IsDirectory(path))
                {
                    var ages = FsTree.Ages(path, cx.Limit);
                    kept = ages.TimedOut
                        ? JunkEvidence.ScanStopped
                        : JunkCache.KeptBecause(
                            ages.Newest,
                            ages.Incomplete,
                            false,
                            false,
                            minAge,
                            cx.Now
                        );
                }
                else
                {
                    kept =
                        JunkCache.KeptBecause(
                            new FileInfo(path).LastWriteTimeUtc,
                            false,
                            false,
                            false,
                            minAge,
                            cx.Now
                        ) ?? HeldReason(path);
                }
                items.Add(JunkCache.Make(kind, path, evidence, kept, cx));
            }
        }
        return items;
    }

    /// <summary>
    /// Build output in agent worktrees: a tagged cache (<c>target/</c>) by T152's rules; untagged
    /// <c>dist/</c>, <c>.next/</c>, <c>__pycache__/</c> and dependency folders are listed read-only.
    /// </summary>
    internal static List<JunkItem> BuildItems(IEnumerable<string> worktrees, JunkContext cx)
    {
        ArgumentNullException.ThrowIfNull(worktrees);
        ArgumentNullException.ThrowIfNull(cx);
        var items = new List<JunkItem>();
        foreach (var wt in worktrees)
        {
            var tagged = JunkCache.Tagged("build", wt, cx).ToList();
            items.AddRange(tagged);
            var paths = tagged.Select(t => t.Path).ToHashSet(StringComparer.Ordinal);
            var finds = Find(
                wt,
                cx.Limit,
                paths.Contains,
                (name, _, dir, _) =>
                    !dir ? null
                    : BuildDirs.Contains(name) ? "build"
                    : Deps.Any(d => string.Equals(d.Dir, name, StringComparison.Ordinal)) ? "deps"
                    : null
            );
            // A tagged `.venv` (uv writes the tag) is already `build` above.
            foreach (var (path, kind) in finds.Where(f => !paths.Contains(f.Path)))
            {
                items.Add(
                    kind is "deps"
                        ? JunkCache.Make(
                            kind,
                            path,
                            JunkEvidence.None,
                            DepsReason(path),
                            cx,
                            "review"
                        )
                        : JunkCache.Make(
                            kind,
                            path,
                            JunkEvidence.None,
                            JunkEvidence.NotDocumented,
                            cx
                        )
                );
            }
        }
        return items;
    }

    private static string DepsReason(string path)
    {
        var name = Path.GetFileName(path);
        var beside = Path.GetDirectoryName(path) ?? path;
        var manifests =
            Deps.FirstOrDefault(d => string.Equals(d.Dir, name, StringComparison.Ordinal)).Manifests
            ?? [];
        return manifests.Any(m => File.Exists(Path.Combine(beside, m)))
            ? JunkEvidence.NotDocumented
            : "no lockfile or manifest beside it: cannot be reinstalled";
    }

    /// <summary>
    /// Version folders of rtok's plugin Claude Code unpacked and no longer uses: every one under
    /// the plugin cache that <c>installed_plugins.json</c> names as no <c>installPath</c>. A file
    /// that is missing or names no rtok install says nothing about use, so then none is listed.
    /// </summary>
    internal static List<JunkOwned> StagingCaches(string claude)
    {
        ArgumentException.ThrowIfNullOrEmpty(claude);
        var used = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(claude, "plugins", "installed_plugins.json"))
            );
            if (
                doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("plugins", out var plugins)
                && plugins.ValueKind == JsonValueKind.Object
                && plugins.TryGetProperty(PluginId, out var installs)
                && installs.ValueKind == JsonValueKind.Array
            )
            {
                foreach (var i in installs.EnumerateArray())
                {
                    if (
                        i.ValueKind == JsonValueKind.Object
                        && i.TryGetProperty("installPath", out var p)
                        && p.GetString() is { Length: > 0 } s
                    )
                    {
                        used.Add(JunkTree.Real(s));
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
        if (used.Count == 0)
        {
            return [];
        }
        var cache = Path.Combine(claude, PluginCache.Replace('/', Path.DirectorySeparatorChar));
        return
        [
            .. JunkTree
                .Children(cache)
                .Where(p => JunkTree.IsDirectory(p) && !used.Contains(JunkTree.Real(p)))
                .Select(p => new JunkOwned(p, JunkEvidence.RtokOwn, true)),
        ];
    }

    /// <summary>
    /// The <c>&lt;commit&gt;</c> folders of a per-commit cache that are not the newest: the folder
    /// touched last stands for the running build and stays. T152's idle rule still applies.
    /// </summary>
    internal static List<JunkOwned> StaleCodeCaches(string dir)
    {
        var dirs = JunkTree
            .Children(dir)
            .Where(JunkTree.IsDirectory)
            .Select(p => (Path: p, Modified: Directory.GetLastWriteTimeUtc(p)))
            .ToList();
        if (dirs.Count == 0)
        {
            return [];
        }
        var newest = dirs.Max(d => d.Modified);
        return
        [
            .. dirs.Where(d => d.Modified < newest)
                .Select(d => new JunkOwned(d.Path, JunkEvidence.Section22, true)),
        ];
    }
}
