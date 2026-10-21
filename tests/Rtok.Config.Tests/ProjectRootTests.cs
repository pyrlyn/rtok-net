using Rtok.Testing;

namespace Rtok.Config.Tests;

public sealed class ProjectRootTests
{
    [Fact]
    public void A_git_directory_above_the_cwd_is_the_root()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("repo", ".git"));
        Directory.CreateDirectory(dir.Combine("repo", "a", "b"));

        var root = ProjectRoot.Find(dir.Combine("repo", "a", "b"));

        Assert.Equal(
            (dir.Combine("repo"), dir.Combine("repo"), dir.Combine("repo", ".git")),
            (root?.Checkout, root?.Main, root?.GitDir)
        );
    }

    [Fact]
    public void A_worktree_reads_its_main_checkout_through_commondir()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("main", ".git", "worktrees", "wt"));
        dir.Write("main/.git/worktrees/wt/commondir", "../..\n");
        dir.Write("wt/.git", $"gitdir: {dir.Combine("main", ".git", "worktrees", "wt")}\n");

        var root = ProjectRoot.Find(dir.Combine("wt"));

        Assert.Equal((dir.Combine("wt"), dir.Combine("main")), (root?.Checkout, root?.Main));
    }

    [Fact]
    public void Outside_a_checkout_there_is_no_root()
    {
        using var dir = new TempDir();

        Assert.Null(ProjectRoot.Find(dir.Path));
    }
}
