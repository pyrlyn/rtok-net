using LibGit2Sharp;
using Rtok.Testing;

namespace Rtok.Git.Tests;

public sealed class GitRepositoryTests
{
    [Fact]
    public void Find_top_walks_up_to_a_git_directory_or_a_linked_worktree_file()
    {
        using var dir = new TempDir("rtok-git-");
        var deep = Directory.CreateDirectory(dir.Combine("repo", "src", "deep")).FullName;
        Assert.Null(GitRepository.FindTop(deep));

        Directory.CreateDirectory(dir.Combine("repo", ".git"));
        Assert.Equal(dir.Combine("repo"), GitRepository.FindTop(deep));
        Assert.Equal(dir.Combine("repo"), GitRepository.FindTop(dir.Combine("repo")));

        File.WriteAllText(dir.Combine("repo", "src", ".git"), "gitdir: /elsewhere\n");
        Assert.Equal(dir.Combine("repo", "src"), GitRepository.FindTop(deep));
    }

    [Fact]
    public void Open_reads_the_repository_from_a_subdirectory_and_nothing_outside_one()
    {
        using var dir = new TempDir("rtok-git-");
        Assert.Null(GitRepository.Open(dir.Path));
        Repository.Init(dir.Path);
        File.WriteAllText(dir.Combine("a.txt"), "a\n");
        using (var repo = new Repository(dir.Path))
        {
            Commands.Stage(repo, "a.txt");
            var who = new Signature("t", "t@t", DateTimeOffset.UnixEpoch);
            repo.Commit("one", who, who);
        }

        var sub = Directory.CreateDirectory(dir.Combine("src")).FullName;
        using var opened = GitRepository.Open(sub);

        Assert.NotNull(opened);
        Assert.Equal("one", opened.Head.Tip.MessageShort);
    }
}
