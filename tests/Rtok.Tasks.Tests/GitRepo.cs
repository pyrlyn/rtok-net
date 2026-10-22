using LibGit2Sharp;

namespace Rtok.Tasks.Tests;

/// <summary>Real repositories for tests, made through LibGit2Sharp; git is never run.</summary>
internal static class GitRepo
{
    /// <summary>Makes <paramref name="path"/> a repository, with an <c>origin</c> when given.</summary>
    public static void Init(string path, string? origin = null)
    {
        Repository.Init(path);
        if (origin is not null)
        {
            using var repo = new Repository(path);
            repo.Network.Remotes.Add("origin", origin);
        }
    }
}
