using LibGit2Sharp;

namespace Rtok.Git;

/// <summary>
/// Opens the repository a path lies in (N21). Use it instead of <c>Repository.Discover</c>,
/// which returns <see langword="null"/> on NativeAOT with LibGit2Sharp 0.32.0 although
/// <c>new Repository(path)</c> works there; <c>Rtok.AotTests</c> <c>GitTests</c> pins both.
/// </summary>
public static class GitRepository
{
    /// <summary>The nearest directory at or above <paramref name="path"/> that holds a
    /// <c>.git</c> directory or a linked worktree's <c>.git</c> file, or
    /// <see langword="null"/>.</summary>
    public static string? FindTop(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        for (var dir = Path.GetFullPath(path); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var git = Path.Join(dir, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                return dir;
            }
        }

        return null;
    }

    /// <summary>The repository whose work tree holds <paramref name="path"/>, or
    /// <see langword="null"/> outside any. The caller disposes it.</summary>
    /// <exception cref="LibGit2SharpException">The repository cannot be opened.</exception>
    public static Repository? Open(string path) =>
        FindTop(path) is { } top ? new Repository(top) : null;
}
