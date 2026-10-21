using Pyrlyn.FsKit;

namespace Rtok.Config;

/// <summary>
/// The git checkout a directory belongs to. Found lexically: <c>.git</c> files and
/// <c>commondir</c> are read, git is never run.
/// </summary>
/// <param name="Checkout">The directory with the nearest <c>.git</c>: a linked worktree's own directory. <c>.rtok.toml</c> lives here.</param>
/// <param name="Main">The main checkout, the parent of the common <c>.git</c>. Equal to <paramref name="Checkout"/> outside a linked worktree.</param>
/// <param name="GitDir">The common <c>.git</c> directory.</param>
public sealed record ProjectRoot(string Checkout, string Main, string GitDir)
{
    /// <summary>The project <paramref name="cwd"/> is in.</summary>
    /// <param name="cwd">An absolute directory.</param>
    /// <returns>The project, or null outside a git checkout.</returns>
    public static ProjectRoot? Find(string cwd)
    {
        ArgumentNullException.ThrowIfNull(cwd);
        for (var dir = cwd; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var dot = Path.Combine(dir, ".git");
            string gitDir;
            if (File.Exists(dot))
            {
                // A linked worktree's `.git` names its admin dir, whose `commondir` names the
                // main checkout's `.git`. Any other `.git` file (a submodule) is its own project.
                gitDir = CommonDir(dir, File.ReadAllText(dot)) ?? dot;
            }
            else if (Directory.Exists(dot))
            {
                gitDir = dot;
            }
            else
            {
                continue;
            }
            return Path.GetDirectoryName(gitDir) is { } main
                ? new ProjectRoot(dir, main, gitDir)
                : null;
        }
        return null;
    }

    /// <summary><c>&lt;main&gt;/.git</c> for a worktree; both paths may be relative.</summary>
    private static string? CommonDir(string root, string dotGit)
    {
        var line = dotGit
            .Split('\n')
            .FirstOrDefault(l => l.StartsWith("gitdir:", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }
        var gitdir = FsPath.Normalize(root, line["gitdir:".Length..].Trim());
        var commondir = Path.Combine(gitdir, "commondir");
        return File.Exists(commondir)
            ? FsPath.Normalize(gitdir, File.ReadAllText(commondir).Trim())
            : null;
    }
}
