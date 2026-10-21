namespace Pyrlyn.FsKit.Tests;

public sealed class FsTreeTests : IDisposable
{
    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void CountsFilesNotDirectoriesAndStopsAtTheTimeLimit()
    {
        dir.Write("a/x", "x");
        dir.Write("a/b/y", new string('y', 5000));
        var usage = FsTree.Usage(dir.Root);
        Assert.Equal(2, usage.Files);
        Assert.True(usage.Bytes >= 5000, usage.ToString());
        Assert.False(usage.TimedOut || usage.Denied);
        Assert.True(FsTree.Usage(dir.Root, TimeSpan.Zero).TimedOut);
        Assert.Equal(default, FsTree.Usage(dir["missing"]));
    }

    [Fact]
    public void CountsAllocatedBlocksNotApparentSize()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "NTFS files are not sparse unless marked");
        var f = dir["sparse"];
        using (var stream = File.Create(f))
        {
            stream.SetLength(64L << 20);
        }
        // A 64 MiB hole allocates next to nothing on every filesystem that supports sparse files.
        Assert.True(FsTree.Usage(f).Bytes < 1 << 20, FsTree.Usage(f).ToString());
    }

    [Fact]
    public void AHardLinkCountsOnce()
    {
        dir.Write("t/a", new string('a', 100_000));
        var one = FsTree.Usage(dir["t"]);
        dir.HardLink("t/a", "t/b");
        var two = FsTree.Usage(dir["t"]);
        Assert.Equal(2, two.Files);
        Assert.Equal(one.Bytes, two.Bytes);
    }

    [Fact]
    public void AHardLinkOutsideTheTreeStillCountsInside()
    {
        dir.Write("t/a", new string('a', 100_000));
        dir.HardLink("t/a", "outside");
        Assert.True(FsTree.Usage(dir["t"]).Bytes >= 100_000);
    }

    [Fact]
    public void ASymlinkIsNeverFollowed()
    {
        dir.Write("big/f", new string('f', 200_000));
        Directory.CreateDirectory(dir["t"]);
        var before = FsTree.Usage(dir["t"]);
        dir.Symlink("t/link", dir["big"]);
        var after = FsTree.Usage(dir["t"]);
        Assert.Equal(before.Files + 1, after.Files);
        Assert.True(after.Bytes - before.Bytes < 100_000, after.ToString());
    }

    [Fact]
    public void AnApfsCloneSharesItsBlocks()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "APFS clones");
        dir.Write("t/a", new string('a', 1_000_000));
        var one = FsTree.Usage(dir["t"]);
        // .NET clones on macOS where the volume can (clonefile).
        File.Copy(dir["t/a"], dir["t/b"]);
        var two = FsTree.Usage(dir["t"]);
        Assert.True(two.Bytes - one.Bytes < 100_000, $"{one} then {two}");
    }

    [Fact]
    public void FileIdentityMatchesAcrossHardLinksAndLinks()
    {
        var a = dir.Write("a", "x");
        dir.HardLink("a", "b");
        var c = dir.Write("c", "x");
        Assert.Equal(FileIdentity.Of(a), FileIdentity.Of(dir["b"]));
        Assert.NotEqual(FileIdentity.Of(a), FileIdentity.Of(c));
        Assert.True(FileIdentity.Same(a, dir["b"]));
        Assert.False(FileIdentity.Same(a, dir["missing"]));
        Assert.Throws<FileNotFoundException>(() => FileIdentity.Of(dir["missing"]));

        var link = dir.Symlink("link", a);
        Assert.Equal(FileIdentity.Of(a), FileIdentity.Of(link));
        Assert.NotEqual(FileIdentity.Of(a), FileIdentity.Of(link, followLinks: false));
    }
}
