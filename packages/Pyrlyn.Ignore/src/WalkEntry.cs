namespace Pyrlyn.Ignore;

/// <summary>One file, directory or symlink a <see cref="RepoWalker"/> reached.</summary>
/// <param name="Path">The full path.</param>
/// <param name="RelativePath">The path relative to the walk root, <c>/</c>-separated.</param>
/// <param name="IsDirectory">A directory (never a symlink to one: links are not followed).</param>
/// <param name="IsSymlink">A symbolic link, to a file or a directory.</param>
/// <param name="Depth">1 for the root's children.</param>
public readonly record struct WalkEntry(
    string Path,
    string RelativePath,
    bool IsDirectory,
    bool IsSymlink,
    int Depth
);
