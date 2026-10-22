using Rtok.Testing;

namespace Rtok.Plugins.Graph.Tests;

// Git reads what `git rev-parse HEAD` and `git log --name-only --relative --no-renames` print:
// loose and packed refs, a detached HEAD, a linked worktree and a subdirectory root.
public sealed class GitTests
{
    private static string Log(string root) =>
        string.Join(
            " | ",
            Git.CommitFiles(root, 10)!
                .Select(f => string.Join(' ', f.Order(StringComparer.Ordinal)))
        );

    [Fact]
    public void Loose_and_packed_refs_and_a_detached_head_match_rev_parse()
    {
        using var h = new GraphHarness();
        TestGit.Init(h);
        TestGit.Commit(h, "one", "a.rs");
        var dir = h.Project.Path;
        var sub = Directory.CreateDirectory(h.Project.Combine("src", "deep")).FullName;

        Assert.Equal(TestGit.Run(dir, "rev-parse", "HEAD"), Git.Head(dir));
        Assert.Equal(Git.Head(dir), Git.Head(sub));
        TestGit.Run(dir, "pack-refs", "--all");
        Assert.False(File.Exists(h.Project.Combine(".git", "refs", "heads", "main")));
        Assert.Equal(TestGit.Run(dir, "rev-parse", "HEAD"), Git.Head(dir));
        TestGit.Commit(h, "two", "a.rs");
        TestGit.Run(dir, "checkout", "-q", "--detach", "HEAD~1");
        Assert.Equal(TestGit.Run(dir, "rev-parse", "HEAD"), Git.Head(dir));
    }

    [Fact]
    public void A_linked_worktree_reads_its_own_head()
    {
        using var h = new GraphHarness();
        TestGit.Init(h);
        TestGit.Commit(h, "one", "a.rs");
        TestGit.Run(h.Project.Path, "branch", "side");
        TestGit.Commit(h, "two", "a.rs");
        using var linked = new TempDir("rtok-graph-wt-");
        var wt = Path.Join(linked.Path, "wt");
        TestGit.Run(h.Project.Path, "worktree", "add", "-q", wt, "side");

        Assert.Equal(TestGit.Run(wt, "rev-parse", "HEAD"), Git.Head(wt));
        Assert.NotEqual(Git.Head(h.Project.Path), Git.Head(wt), StringComparer.Ordinal);
    }

    [Fact]
    public void An_unborn_branch_or_no_repository_has_no_head()
    {
        using var h = new GraphHarness();
        Assert.Null(Git.Head(h.Project.Path));
        Assert.Null(Git.CommitFiles(h.Project.Path, 10));
        TestGit.Init(h);
        Assert.Null(Git.Head(h.Project.Path));
    }

    // Newest first; the rename is a deletion and an addition; the merge lists nothing; paths
    // outside a subdirectory root are left out and the rest lose its prefix; names stay literal.
    [Fact]
    public void Commit_files_match_git_log_name_only_relative()
    {
        using var h = new GraphHarness();
        var dir = h.Project.Path;
        TestGit.Init(h);
        TestGit.Commit(h, "one", "top.rs", "src/a.rs");
        TestGit.Run(dir, "checkout", "-q", "-b", "side");
        TestGit.Commit(h, "side", "src/é.rs");
        TestGit.Run(dir, "checkout", "-q", "main");
        TestGit.Run(dir, "mv", "src/a.rs", "src/b.rs");
        TestGit.Run(dir, "commit", "-q", "-m", "move");
        TestGit.Run(dir, "merge", "-q", "--no-ff", "-m", "merge", "side");

        Assert.Equal(" | a.rs b.rs | é.rs | a.rs", Log(h.Root + "/src"));
        Assert.Equal(" | src/a.rs src/b.rs | src/é.rs | src/a.rs top.rs", Log(h.Root));
        Assert.Equal(2, Git.CommitFiles(h.Root, 2)!.Count);
    }
}
