using Pyrlyn.FsKit;

namespace Rtok.Plugins.Graph;

/// <summary>The one choke point for every way into the index: the CLI, the tools, the watcher
/// and auto-index all pass a root through <see cref="Check"/>.</summary>
internal static class GraphRoot
{
    /// <summary>The canonical form of <paramref name="root"/>, the key its rows live under: a
    /// root reached through a symlink shares the index of the real directory.</summary>
    /// <exception cref="GraphRootException"><paramref name="root"/> is <c>/</c> or the home
    /// directory.</exception>
    public static string Check(string root, string? home = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        var canon = Path.TrimEndingDirectorySeparator(FsPath.Canon(Path.GetFullPath(root)));
        return FsPath.IsUnwalkableRoot(
            canon,
            home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        )
            ? throw new GraphRootException(canon)
            : canon;
    }

    /// <summary><see cref="Check"/>, or <see langword="null"/> for a refused root: for an
    /// answer that needs no index, only its pending banner.</summary>
    public static string? TryCheck(string root)
    {
        try
        {
            return Check(root);
        }
        catch (GraphRootException)
        {
            return null;
        }
    }
}
