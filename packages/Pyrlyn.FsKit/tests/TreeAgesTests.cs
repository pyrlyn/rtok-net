namespace Pyrlyn.FsKit.Tests;

public sealed class TreeAgesTests : IDisposable
{
    private static readonly DateTime Old = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    private string Aged(string relative, DateTime when)
    {
        var path = dir.Write(relative, "x");
        File.SetLastWriteTimeUtc(path, when);
        return path;
    }

    [Fact]
    public void TheNewestFileWinsAndDirectoriesDoNotCount()
    {
        Aged("t/a", Old);
        Aged("t/sub/b", Old.AddDays(3));

        var ages = FsTree.Ages(dir["t"]);

        Assert.Equal(Old.AddDays(3), ages.Newest);
        Assert.False(ages.Incomplete || ages.TimedOut);
        Assert.Empty(ages.Caches);
    }

    [Fact]
    public void ATaggedCacheIsListedWithItsOwnNewest()
    {
        dir.Write("t/c/CACHEDIR.TAG", CacheDirTag.Signature);
        File.SetLastWriteTimeUtc(dir["t/c/CACHEDIR.TAG"], Old);
        Aged("t/c/blob", Old.AddDays(1));
        Aged("t/other", Old.AddDays(5));

        var ages = FsTree.Ages(dir["t"]);

        var cache = Assert.Single(ages.Caches);
        Assert.Equal(dir["t/c"], cache.Path);
        Assert.Equal(Old.AddDays(1), cache.Newest);
        Assert.Equal(Old.AddDays(5), ages.Newest);
    }

    [Fact]
    public void ANestedCheckoutIsItsOwnTree()
    {
        Aged("t/a", Old);
        Directory.CreateDirectory(dir["t/repo/.git"]);
        Aged("t/repo/new", Old.AddDays(9));

        Assert.Equal(Old, FsTree.Ages(dir["t"]).Newest);
    }

    [Fact]
    public void ALinkCountsItsOwnTimeNotItsTarget()
    {
        Aged("t/a", Old);
        Aged("far/new", Old.AddDays(9));
        dir.Symlink("t/link", dir["far"]);

        var newest = FsTree.Ages(dir["t"]).Newest;

        Assert.NotEqual(Old.AddDays(9), newest);
    }

    [Fact]
    public void NothingThereAndTheTimeLimitAreEmptyAnswers()
    {
        Aged("t/a", Old);

        Assert.Null(FsTree.Ages(dir["missing"]).Newest);
        Assert.True(FsTree.Ages(dir["t"], TimeSpan.Zero).TimedOut);
        Assert.Equal(Old, FsTree.Ages(dir["t/a"]).Newest);
    }
}
