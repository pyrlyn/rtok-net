using Pyrlyn.FsKit;
using Rtok.Testing;

namespace Rtok.Hooks.Tests;

// The install keeps rtok and rtok-hook together; a bin directory on PATH links rtok there.
public sealed class HookClientLinkTests : IDisposable
{
    private readonly TempDir _dir = new("rl-");

    public HookClientLinkTests()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("no links on Windows");
        }
        _dir.Write("install/rtok", "rtok");
        _dir.Write("install/rtok-hook", "client");
        Directory.CreateDirectory(_dir.Combine("bin"));
        File.CreateSymbolicLink(_dir.Combine("bin", "rtok"), _dir.Combine("install", "rtok"));
    }

    private string Bin(string name = "") => _dir.Combine("bin", name);

    // Links point at the client's real path (on macOS the temp dir is under /private).
    private string Client => FsPath.Canonicalize(_dir.Combine("install", "rtok-hook"));

    private HookLinkResult Ensure(string? path = null) =>
        HookClientLink.Ensure(path ?? Bin(), _dir.Combine("install", "rtok"));

    [Fact]
    public void It_links_the_client_beside_rtok_on_path()
    {
        Assert.Equal(HookLinkResult.Linked, Ensure());

        Assert.Equal(Client, new FileInfo(Bin("rtok-hook")).LinkTarget);
        Assert.Equal(HookLinkResult.AlreadyLinked, Ensure());
        Assert.Empty(Directory.EnumerateFileSystemEntries(Bin(), ".*"));
    }

    [Fact]
    public void Only_the_first_rtok_on_path_counts()
    {
        _dir.Write("other/rtok", "another build");

        Assert.Equal(
            HookLinkResult.NotRunningExe,
            Ensure(_dir.Combine("other") + Path.PathSeparator + Bin())
        );
        Assert.False(File.Exists(Bin("rtok-hook")));
    }

    [Fact]
    public void Without_rtok_on_path_nothing_is_linked() =>
        Assert.Equal(HookLinkResult.NotOnPath, Ensure(_dir.Combine("install-not-here")));

    [Fact]
    public void Without_a_client_beside_rtok_nothing_is_linked()
    {
        File.Delete(_dir.Combine("install", "rtok-hook"));

        Assert.Equal(HookLinkResult.NoClient, Ensure());
    }

    [Fact]
    public void A_regular_file_is_never_replaced()
    {
        _dir.Write("bin/rtok-hook", "mine");

        Assert.Equal(HookLinkResult.Foreign, Ensure());
        Assert.Equal("mine", File.ReadAllText(Bin("rtok-hook")));
    }

    [Fact]
    public void A_link_to_something_else_is_never_replaced()
    {
        _dir.Write("tools/wrapper", "mine");
        File.CreateSymbolicLink(Bin("rtok-hook"), _dir.Combine("tools", "wrapper"));

        Assert.Equal(HookLinkResult.Foreign, Ensure());
        Assert.Equal(_dir.Combine("tools", "wrapper"), new FileInfo(Bin("rtok-hook")).LinkTarget);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_dangling_or_other_version_client_link_is_re_pointed(bool dangling)
    {
        var old = _dir.Combine("old", "rtok-hook");
        if (!dangling)
        {
            _dir.Write("old/rtok-hook", "old client");
        }
        File.CreateSymbolicLink(Bin("rtok-hook"), old);

        Assert.Equal(HookLinkResult.Relinked, Ensure());
        Assert.Equal(Client, new FileInfo(Bin("rtok-hook")).LinkTarget);
    }

    public void Dispose() => _dir.Dispose();
}
