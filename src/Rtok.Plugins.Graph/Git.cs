using LibGit2Sharp;
using Pyrlyn.FsKit;
using Rtok.Git;

namespace Rtok.Plugins.Graph;

/// <summary>The head line and co-change through LibGit2Sharp (N21). A root in no repository, a
/// bare repository, a missing libgit2 or a failing read is <see langword="null"/>: the answers
/// carry no git line then, and nothing else changes.</summary>
internal static class Git
{
    /// <summary>The commit HEAD names, or <see langword="null"/> (no repository, unborn
    /// branch).</summary>
    public static string? Head(string root) => Open(root, repo => repo.Head.Tip?.Sha);

    /// <summary>The files of the last <paramref name="count"/> commits from HEAD, as
    /// <c>git log --name-only --relative --no-renames</c> lists them: paths under
    /// <paramref name="root"/>, relative to it, a merge with none and a root commit with all of
    /// its own.</summary>
    public static List<List<string>>? CommitFiles(string root, int count) =>
        Open(
            root,
            repo =>
                repo.Info.WorkingDirectory is { } work
                    ? Walk(repo, Prefix(work, root), count)
                    : null
        );

    private static List<List<string>> Walk(Repository repo, string prefix, int count)
    {
        var options = new CompareOptions { Similarity = SimilarityOptions.None };
        var commits = new List<List<string>>();
        foreach (var commit in repo.Commits.Take(count))
        {
            var parents = commit.Parents.Take(2).ToList();
            if (parents.Count > 1)
            {
                commits.Add([]);
                continue;
            }

            using var changes = repo.Diff.Compare<TreeChanges>(
                parents.Count == 0 ? null : parents[0].Tree,
                commit.Tree,
                options
            );
            commits.Add([
                .. changes
                    .Select(c => c.Path.Replace('\\', '/'))
                    .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(p => p[prefix.Length..]),
            ]);
        }

        return commits;
    }

    // "" at the work tree's top, else "src/sub/" as --relative strips it.
    private static string Prefix(string work, string root)
    {
        var relative = Path.GetRelativePath(FsPath.Canon(work), root).Replace('\\', '/');
        return relative is "." ? "" : relative + "/";
    }

    private static T? Open<T>(string root, Func<Repository, T?> read)
        where T : class
    {
        try
        {
            using var repo = GitRepository.Open(root);
            return repo is null ? null : read(repo);
        }
#pragma warning disable CA1031 // A repository that cannot be read is an answer without a git line.
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
