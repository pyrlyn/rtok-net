using LibGit2Sharp;
using Rtok.Git;

namespace Rtok.AotTests;

// LibGit2Sharp on NativeAOT (N21): Repository.Discover finds nothing, while GitRepository opens
// the same repository from a subdirectory. When a LibGit2Sharp release fixes Discover, the
// first assertion fails; GitRepository can then call it.
public sealed class GitTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("rtok-aot-git-");

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public void Discover_is_null_on_native_aot_and_git_repository_opens_instead()
    {
        Repository.Init(_dir.FullName);
        var sub = _dir.CreateSubdirectory("src").FullName;

        Assert.Null(Repository.Discover(sub));
        using var repo = GitRepository.Open(sub);
        Assert.NotNull(repo);
        Assert.True(repo.Info.IsHeadUnborn);
    }
}
