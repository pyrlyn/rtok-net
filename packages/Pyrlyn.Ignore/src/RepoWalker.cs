using Pyrlyn.FsKit;

namespace Pyrlyn.Ignore;

/// <summary>
/// Walks a directory the way git sees it: <c>.gitignore</c> at every level, <c>.ignore</c>,
/// <c>info/exclude</c> and the global excludes file, with git's precedence (the deepest file
/// that names a path decides; within a file the last rule). An ignored directory is not entered,
/// so nothing below it can be re-included, as in git. Symlinks are yielded, never followed.
/// </summary>
/// <remarks>
/// Precedence, highest first: <see cref="WalkOptions.Exclude"/>, <c>.ignore</c>,
/// <c>.gitignore</c>, <c>info/exclude</c>, the global excludes file. A nested repository (a
/// directory with its own <c>.git</c>) starts a new git scope: the git sources above it stop
/// applying inside it, <c>.ignore</c> files still do.
/// </remarks>
public sealed class RepoWalker
{
    private static readonly char[] GlobChars = ['*', '?', '[', '\\'];

    private readonly WalkOptions options;
    private readonly IgnoreFile? exclude;
    private readonly IgnoreFile? include;
    private readonly string[] includeStarts;
    private readonly Layer rootLayer;
    private readonly Lazy<string?> globalText;

    /// <summary>A walker for <paramref name="root"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="root"/> is the filesystem root or the home directory.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="root"/> is not a directory.</exception>
    public RepoWalker(string root, WalkOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        this.options = options ?? WalkOptions.Default;
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (FsPath.IsUnwalkableRoot(Root, this.options.Home))
        {
            throw new ArgumentException(
                $"{Root}: refusing to walk the filesystem root or the home directory",
                nameof(root)
            );
        }
        if (!Directory.Exists(Root))
        {
            throw new DirectoryNotFoundException($"{Root}: not a directory");
        }
        var rootSlashed = IgnoreFile.Slashes(Root);
        exclude =
            this.options.Exclude.Count == 0
                ? null
                : IgnoreFile.FromLines(rootSlashed, this.options.Exclude, this.options.IgnoreCase);
        include =
            this.options.Include.Count == 0
                ? null
                : IgnoreFile.FromLines(
                    rootSlashed,
                    this.options.Include.Select(p => p.EndsWith('/') ? p + "**" : p),
                    this.options.IgnoreCase
                );
        includeStarts = IncludeStarts(this.options.Include);
        globalText = new Lazy<string?>(ReadGlobal);
        rootLayer = RootLayer();
    }

    /// <summary>The walk root, absolute.</summary>
    public string Root { get; }

    /// <summary>
    /// Every entry below the root, depth first in ordinal name order: the ordinary walk, then the
    /// files <see cref="WalkOptions.Include"/> names that it skipped, each once. The root itself
    /// is not yielded.
    /// </summary>
    public IEnumerable<WalkEntry> Walk()
    {
        var seen = include is null ? null : new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in WalkFrom(Root, 0, rootLayer))
        {
            if (seen is not null && !entry.IsDirectory && IsNamedByInclude(entry))
            {
                seen.Add(entry.Path);
            }
            yield return entry;
        }
        if (seen is null)
        {
            yield break;
        }
        foreach (var start in includeStarts)
        {
            var dir = start.Length == 0 ? Root : Path.Combine(Root, start);
            var depth = start.Length == 0 ? 0 : start.Split('/').Length;
            IEnumerable<WalkEntry> found;
            if (Directory.Exists(dir) && !IsSymlink(dir))
            {
                found = WalkFrom(dir, depth, layer: null);
            }
            else if (File.Exists(dir) && Entry(new FileInfo(dir), depth) is { } file)
            {
                found = [file];
            }
            else
            {
                continue;
            }
            foreach (var entry in found)
            {
                if (!entry.IsDirectory && IsNamedByInclude(entry) && seen.Add(entry.Path))
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>
    /// Whether <see cref="Walk"/> yields <paramref name="path"/>, for judging a watcher event
    /// without walking: every directory on the way down from the root is checked too.
    /// </summary>
    public bool IsWalked(string path, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var full = Path.GetFullPath(path, Root);
        var relative = Path.GetRelativePath(Root, full);
        if (
            string.Equals(relative, ".", StringComparison.Ordinal)
            || relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
        )
        {
            return false;
        }
        var parts = IgnoreFile.Slashes(relative).Split('/');
        var layer = rootLayer;
        var dir = Root;
        var walked = true;
        var underGit = false;
        for (var i = 0; i < parts.Length; i++)
        {
            var name = parts[i];
            var child = Path.Combine(dir, name);
            var last = i == parts.Length - 1;
            var childIsDir = !last || isDirectory;
            if (
                string.Equals(name, ".git", StringComparison.Ordinal)
                || (childIsDir && IsLinkedWorktree(child))
            )
            {
                underGit = true;
                walked = false;
                break;
            }
            if (Skipped(name, child, childIsDir, layer, ignoreFiles: true))
            {
                walked = false;
                break;
            }
            if (!last)
            {
                layer = Load(child, layer);
                dir = child;
            }
        }
        if (walked)
        {
            return true;
        }
        return !isDirectory
            && !underGit
            && include is not null
            && include.Match(full, false) == IgnoreMatch.Ignore
            && !IsExcludedOnTheWay(parts, full);
    }

    private bool IsExcludedOnTheWay(string[] parts, string full)
    {
        if (exclude is null)
        {
            return false;
        }
        var dir = Root;
        for (var i = 0; i < parts.Length; i++)
        {
            dir = Path.Combine(dir, parts[i]);
            if (exclude.Match(dir, i < parts.Length - 1) == IgnoreMatch.Ignore)
            {
                return true;
            }
        }
        return exclude.Match(full, false) == IgnoreMatch.Ignore;
    }

    private bool IsNamedByInclude(WalkEntry entry) =>
        include!.Match(entry.Path, entry.IsDirectory) == IgnoreMatch.Ignore;

    /// <summary>Depth-first, ordinal order; <paramref name="layer"/> <see langword="null"/> walks with the ignore files off.</summary>
    private IEnumerable<WalkEntry> WalkFrom(string start, int startDepth, Layer? layer)
    {
        var ignoreFiles = layer is not null;
        var pending = new Stack<(IEnumerator<FileSystemInfo> Children, int Depth, Layer? Layer)>();
        pending.Push((Children(start), startDepth, layer));
        try
        {
            while (pending.Count > 0)
            {
                var (children, depth, dirLayer) = pending.Peek();
                if (!children.MoveNext())
                {
                    pending.Pop().Children.Dispose();
                    continue;
                }
                var info = children.Current;
                if (
                    Entry(info, depth + 1) is not { } entry
                    || Skipped(info.Name, entry.Path, entry.IsDirectory, dirLayer, ignoreFiles)
                    || (
                        entry.IsDirectory
                        && options.SkipLinkedWorktrees
                        && IsLinkedWorktree(entry.Path)
                    )
                )
                {
                    continue;
                }
                yield return entry;
                if (entry.IsDirectory && (options.MaxDepth is not { } max || entry.Depth < max))
                {
                    pending.Push(
                        (
                            Children(entry.Path),
                            entry.Depth,
                            ignoreFiles ? Load(entry.Path, dirLayer) : null
                        )
                    );
                }
            }
        }
        finally
        {
            while (pending.Count > 0)
            {
                pending.Pop().Children.Dispose();
            }
        }
    }

    /// <summary>The entries of <paramref name="dir"/> in ordinal name order; none when it cannot be read.</summary>
    private static IEnumerator<FileSystemInfo> Children(string dir)
    {
        try
        {
            return new DirectoryInfo(dir)
                .EnumerateFileSystemInfos()
                .OrderBy(i => i.Name, StringComparer.Ordinal)
                .ToList()
                .GetEnumerator();
        }
        catch (IOException)
        {
            return Enumerable.Empty<FileSystemInfo>().GetEnumerator();
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<FileSystemInfo>().GetEnumerator();
        }
    }

    private WalkEntry? Entry(FileSystemInfo info, int depth)
    {
        if (string.Equals(info.Name, ".git", StringComparison.Ordinal))
        {
            return null;
        }
        var symlink =
            info.Attributes.HasFlag(FileAttributes.ReparsePoint) && info.LinkTarget is not null;
        var isDir = !symlink && info is DirectoryInfo;
        var relative = IgnoreFile.Slashes(Path.GetRelativePath(Root, info.FullName));
        return new WalkEntry(info.FullName, relative, isDir, symlink, depth);
    }

    /// <summary>Whether a child is filtered out: hidden, excluded, or ignored by the ignore files of its directory and those above.</summary>
    private bool Skipped(string name, string path, bool isDir, Layer? layer, bool ignoreFiles)
    {
        if (!options.Hidden && name.StartsWith('.'))
        {
            return true;
        }
        if (exclude is not null && exclude.Match(path, isDir) == IgnoreMatch.Ignore)
        {
            return true;
        }
        return ignoreFiles && layer is not null && Ignored(layer, path, isDir);
    }

    private static bool Ignored(Layer layer, string path, bool isDir)
    {
        var dot = IgnoreMatch.None;
        var git = IgnoreMatch.None;
        var info = IgnoreMatch.None;
        var global = IgnoreMatch.None;
        var sawGit = false;
        for (var l = layer; l is not null; l = l.Parent)
        {
            if (dot == IgnoreMatch.None && l.DotIgnore is { } d)
            {
                dot = d.Match(path, isDir);
            }
            if (!sawGit)
            {
                if (git == IgnoreMatch.None && l.GitIgnore is { } g)
                {
                    git = g.Match(path, isDir);
                }
                if (info == IgnoreMatch.None && l.GitExclude is { } x)
                {
                    info = x.Match(path, isDir);
                }
                if (global == IgnoreMatch.None && l.Global is { } gl)
                {
                    global = gl.Match(path, isDir);
                }
            }
            sawGit |= l.GitRoot;
        }
        var decided =
            dot != IgnoreMatch.None ? dot
            : git != IgnoreMatch.None ? git
            : info != IgnoreMatch.None ? info
            : global;
        return decided == IgnoreMatch.Ignore;
    }

    /// <summary>The layers from the repository root (or the walk root) down to the walk root.</summary>
    private Layer RootLayer()
    {
        var repo = GitRepo.Find(Root);
        if (repo is null || !options.Parents || FsPath.SamePath(repo.Root, Root))
        {
            return Load(Root, null, repo);
        }
        var chain = new List<string>();
        for (
            var d = Root;
            d is not null && !FsPath.SamePath(d, repo.Root);
            d = Path.GetDirectoryName(d)
        )
        {
            chain.Add(d);
        }
        var layer = Load(repo.Root, null, repo);
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            layer = Load(chain[i], layer);
        }
        return layer;
    }

    private Layer Load(string dir, Layer? parent) =>
        Load(dir, parent, GitRepo.Has(dir) ? GitRepo.At(dir) : null);

    private Layer Load(string dir, Layer? parent, GitRepo? repo)
    {
        var inGit = repo is not null || parent?.InGit == true;
        var ic = options.IgnoreCase;
        var slashed = IgnoreFile.Slashes(dir);
        IgnoreFile? gitIgnore = null;
        if (options.GitIgnore && (inGit || !options.RequireGit))
        {
            gitIgnore = IgnoreFile.Load(Path.Combine(dir, ".gitignore"), slashed, ic);
        }
        var dotIgnore = options.DotIgnore
            ? IgnoreFile.Load(Path.Combine(dir, ".ignore"), slashed, ic)
            : null;
        IgnoreFile? info = null;
        IgnoreFile? global = null;
        if (repo is not null)
        {
            var repoRoot = IgnoreFile.Slashes(repo.Root);
            if (options.GitExclude && repo.CommonDir is { } common)
            {
                info = IgnoreFile.Load(Path.Combine(common, "info", "exclude"), repoRoot, ic);
            }
            if (options.GlobalExcludes && globalText.Value is { } text)
            {
                global = IgnoreFile.Parse(repoRoot, text, ic);
            }
        }
        else if (
            parent is null
            && !options.RequireGit
            && options.GlobalExcludes
            && globalText.Value is { } text
        )
        {
            global = IgnoreFile.Parse(slashed, text, ic);
        }
        return new Layer(parent, gitIgnore, dotIgnore, info, global, repo is not null, inGit);
    }

    private string? ReadGlobal()
    {
        var path =
            options.GlobalExcludesFile
            ?? (GitRepo.Find(Root) ?? null)?.GlobalExcludesFile(options.Home, options.XdgConfigHome)
            ?? GlobalWithoutRepo();
        if (path is null)
        {
            return null;
        }
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string? GlobalWithoutRepo()
    {
        var xdg =
            !string.IsNullOrEmpty(options.XdgConfigHome) ? options.XdgConfigHome
            : options.Home is { } home ? Path.Combine(home, ".config")
            : null;
        return xdg is null ? null : Path.Combine(xdg, "git", "ignore");
    }

    private static bool IsLinkedWorktree(string dir) =>
        File.Exists(Path.Combine(dir, ".git")) && GitRepo.At(dir) is { IsLinkedWorktree: true };

    private static bool IsSymlink(string path) => new FileInfo(path).LinkTarget is not null;

    /// <summary>
    /// Where each include pattern's walk starts, relative to the root: its leading literal
    /// components when anchored, else the root. A start inside an earlier one is dropped.
    /// </summary>
    private static string[] IncludeStarts(IReadOnlyList<string> patterns)
    {
        var starts = patterns
            .Select(p =>
            {
                var body = p.TrimEnd('/');
                if (!body.Contains('/', StringComparison.Ordinal))
                {
                    return "";
                }
                return string.Join(
                    '/',
                    body.TrimStart('/')
                        .Split('/')
                        .TakeWhile(part => part.Length > 0 && part.IndexOfAny(GlobChars) < 0)
                );
            })
            .Order(StringComparer.Ordinal)
            .ToList();
        var kept = new List<string>();
        foreach (var s in starts)
        {
            if (
                !kept.Exists(k =>
                    k.Length == 0
                    || string.Equals(k, s, StringComparison.Ordinal)
                    || s.StartsWith(k + "/", StringComparison.Ordinal)
                )
            )
            {
                kept.Add(s);
            }
        }
        return [.. kept];
    }

    private sealed record Layer(
        Layer? Parent,
        IgnoreFile? GitIgnore,
        IgnoreFile? DotIgnore,
        IgnoreFile? GitExclude,
        IgnoreFile? Global,
        bool GitRoot,
        bool InGit
    );
}
