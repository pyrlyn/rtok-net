namespace Pyrlyn.FsKit.Tests;

public sealed class CacheDirTagTests : IDisposable
{
    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void OnlyTheSignatureMakesACache()
    {
        dir.Write("ok/CACHEDIR.TAG", CacheDirTag.Signature + "\n# made by a test\n");
        dir.Write("bad/CACHEDIR.TAG", "Signature: 00000000000000000000000000000000");
        dir.Write("short/CACHEDIR.TAG", "Signature:");
        Directory.CreateDirectory(dir["none"]);

        Assert.True(CacheDirTag.IsCacheDir(dir["ok"]));
        Assert.False(CacheDirTag.IsCacheDir(dir["bad"]));
        Assert.False(CacheDirTag.IsCacheDir(dir["short"]));
        Assert.False(CacheDirTag.IsCacheDir(dir["none"]));
        Assert.False(CacheDirTag.IsCacheDir(dir["missing"]));
    }

    [Fact]
    public void ALinkedTagIsNoTag()
    {
        var real = dir.Write("real/CACHEDIR.TAG", CacheDirTag.Signature);
        Directory.CreateDirectory(dir["linked"]);
        dir.Symlink("linked/CACHEDIR.TAG", real);

        Assert.False(CacheDirTag.IsCacheDir(dir["linked"]));
    }
}
