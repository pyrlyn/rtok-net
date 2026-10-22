using Pyrlyn.Ignore;
using Rtok.Config;
using Rtok.Plugins.Read;

namespace Rtok.Plugins.Graph;

/// <summary>
/// What the graph indexes under one root (rtok <c>graph/walk.rs</c>): the repository as git
/// sees it, plus <c>[plugins.graph] include</c>, minus <c>exclude</c>, which wins; files with a
/// tags grammar, built in or mapped by <c>extensions</c>. Linked worktrees below the root are
/// skipped; submodules and nested clones stay.
/// </summary>
internal sealed class Matcher
{
    private readonly RepoWalker _walker;
    private readonly IgnoreFile? _exclude;

    /// <exception cref="GraphRootException"><paramref name="root"/> is <c>/</c> or the home
    /// directory, or not a directory.</exception>
    public Matcher(string root, GraphSection config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Root = GraphRoot.Check(root);
        Extensions = new Dictionary<string, string>(config.Extensions, StringComparer.Ordinal);
        try
        {
            _walker = new RepoWalker(
                Root,
                new WalkOptions { Include = [.. config.Include], Exclude = [.. config.Exclude] }
            );
        }
        catch (Exception e) when (e is ArgumentException or DirectoryNotFoundException)
        {
            throw new GraphRootException(Root, e);
        }

        _exclude =
            config.Exclude.Count == 0
                ? null
                : IgnoreFile.FromLines(Root.Replace('\\', '/'), config.Exclude);
    }

    /// <summary>The canonical root.</summary>
    public string Root { get; }

    /// <summary>The <c>extensions</c> map: suffix → grammar.</summary>
    public IReadOnlyDictionary<string, string> Extensions { get; }

    /// <summary>Every walked entry, directories included.</summary>
    public IEnumerable<WalkEntry> Walk() => _walker.Walk();

    /// <summary>Not excluded and parsed by a tags grammar. A path outside the root is not.</summary>
    public bool Indexable(string path) => !IsExcluded(path) && Outline.Supported(path, Extensions);

    /// <summary>Indexed only because <c>extensions</c> maps its suffix.</summary>
    public bool UsesExtensionMap(string path) =>
        Extensions.ContainsKey(Outline.Extension(path)) && !Outline.Supported(path);

    /// <summary>Whether the walk yields <paramref name="path"/>: the watcher's filter.</summary>
    public bool IsWalked(string path, bool isDirectory) => _walker.IsWalked(path, isDirectory);

    /// <summary>The root-relative <c>/</c> path of <paramref name="path"/>, or
    /// <see langword="null"/> when it is not below the root.</summary>
    public string? Relative(string path)
    {
        var relative = Path.GetRelativePath(Root, Path.GetFullPath(path, Root));
        return
            string.Equals(relative, ".", StringComparison.Ordinal)
            || relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            ? null
            : relative.Replace('\\', '/');
    }

    // Excluded when the path or any directory above it matches an exclude pattern.
    private bool IsExcluded(string path)
    {
        if (Relative(path) is not { } relative)
        {
            return true;
        }

        if (_exclude is null)
        {
            return false;
        }

        if (_exclude.Match(relative, isDirectory: false) is IgnoreMatch.Ignore)
        {
            return true;
        }

        for (var slash = relative.IndexOf('/'); slash > 0; slash = relative.IndexOf('/', slash + 1))
        {
            if (_exclude.Match(relative[..slash], isDirectory: true) is IgnoreMatch.Ignore)
            {
                return true;
            }
        }

        return false;
    }
}
