using LibGit2Sharp;
using Pyrlyn.FsKit;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Tasks.Tests;

public sealed class ProjectKeyTests
{
    [Theory]
    [InlineData("git@github.com:Pyrlyn/rtok.git", "github.com/pyrlyn/rtok")]
    [InlineData("ssh://git@github.com/pyrlyn/rtok", "github.com/pyrlyn/rtok")]
    [InlineData("https://me@github.com/pyrlyn/rtok/", "github.com/pyrlyn/rtok")]
    [InlineData("https://gitlab.example.com/g/sub/n.git", "gitlab.example.com/g/sub/n")]
    [InlineData("https://host/a/b@v2", "host/a/b@v2")]
    [InlineData("ssh://git@git.example.com:2222/me/app.git", "git.example.com:2222/me/app")]
    public void A_network_origin_keys_by_host_and_path(string url, string key) =>
        Assert.Equal(key, ProjectKey.FromUrl(url, "/a/checkout"));

    [Theory]
    [InlineData("file://localhost/a/remote.git", "/a/remote")]
    [InlineData("file:///a/remote.git", "/a/remote")]
    [InlineData("/a/remote.git", "/a/remote")]
    [InlineData("../remote.git", "/a/remote")]
    [InlineData("/a/Remote/", "/a/Remote")]
    public void A_local_origin_keys_by_its_absolute_path(string url, string key)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        Assert.Equal(key, ProjectKey.FromUrl(url, "/a/checkout"));
    }

    [Fact]
    public void Worktrees_share_the_key_of_the_main_checkout_and_name_the_repo()
    {
        using var dir = new TempDir();
        var main = dir.Combine("main");
        GitRepo.Init(main, "git@github.com:Me/Tasks-Demo.git");
        using (var repo = new Repository(main))
        {
            repo.Network.Remotes.Add("upstream", "https://x/y/z");
            var me = new Signature("t", "t@example.com", DateTimeOffset.UnixEpoch);
            repo.Commit("init", me, me, new CommitOptions { AllowEmptyCommit = true });
            repo.Worktrees.Add("wt", dir.Combine("wt"), isLocked: false);
        }

        var root = ProjectRoot.Find(main)!;
        var worktree = ProjectRoot.Find(dir.Combine("wt"))!;

        Assert.Equal("github.com/me/tasks-demo", ProjectKey.Of(root));
        Assert.Equal("github.com/me/tasks-demo", ProjectKey.Of(worktree));
        Assert.Equal("Tasks-Demo", ProjectKey.Name(worktree));
    }

    [Fact]
    public void Without_an_origin_the_main_checkout_is_the_key()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("airtalk", ".git"));

        var root = ProjectRoot.Find(dir.Combine("airtalk"))!;

        Assert.Equal(FsPath.Canon(dir.Combine("airtalk")).Replace('\\', '/'), ProjectKey.Of(root));
        Assert.Equal("airtalk", ProjectKey.Name(root));
    }
}
