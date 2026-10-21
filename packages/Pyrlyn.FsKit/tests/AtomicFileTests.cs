using System.Runtime.Versioning;
using System.Text;

namespace Pyrlyn.FsKit.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void CreatesAndReplacesWithoutLeftovers()
    {
        var f = dir["state.json"];
        AtomicFile.Write(f, "one");
        Assert.Equal("one", File.ReadAllText(f));
        AtomicFile.Write(f, "two");
        Assert.Equal("two", File.ReadAllText(f));
        Assert.Equal(["state.json"], dir.Entries());
    }

    [Fact]
    public void FailedFillKeepsTheOldFileAndRemovesTheTemp()
    {
        var f = dir.Write("a", "old");
        var err = Assert.Throws<InvalidOperationException>(() =>
            AtomicFile.WriteWith(
                f,
                s =>
                {
                    s.Write("half"u8);
                    throw new InvalidOperationException("boom");
                }
            )
        );
        Assert.Equal("boom", err.Message);
        Assert.Equal("old", File.ReadAllText(f));
        Assert.Equal(["a"], dir.Entries());
    }

    [Fact]
    public void MissingParentFailsUnlessAskedToCreateIt()
    {
        var f = dir["x/y/z.toml"];
        var err = Assert.Throws<DirectoryNotFoundException>(() => AtomicFile.Write(f, "v"));
        Assert.Contains("z.toml", err.Message, StringComparison.Ordinal);
        AtomicFile.Write(f, "v", AtomicWriteOptions.Default with { CreateParent = true });
        Assert.Equal("v", File.ReadAllText(f));
    }

    [Fact]
    public void PathWithoutAFileNameIsRefused()
    {
        Assert.Throws<ArgumentException>(() => AtomicFile.Write(dir[".."], "v"));
        Assert.Throws<ArgumentException>(() =>
            AtomicFile.Write(dir.Root + Path.DirectorySeparatorChar, "v")
        );
    }

    [Fact]
    public void NotDurableStillReplaces()
    {
        var f = dir["c"];
        AtomicFile.Write(f, "fast", AtomicWriteOptions.Default with { Durable = false });
        Assert.Equal("fast", File.ReadAllText(f));
    }

    [Fact]
    public void TextIsUtf8WithoutABom()
    {
        var f = dir["u"];
        AtomicFile.Write(f, "é");
        Assert.Equal(Encoding.UTF8.GetBytes("é"), File.ReadAllBytes(f));
    }

    [Fact]
    public void WriteIfChangedCreatesTheParentAndSkipsIdenticalBytes()
    {
        var f = dir["sub/a.txt"];
        Assert.True(AtomicFile.WriteIfChanged(f, "x"u8));
        Assert.False(AtomicFile.WriteIfChanged(f, "x"u8));
        Assert.True(AtomicFile.WriteIfChanged(f, "y"u8));
        Assert.Equal("y", File.ReadAllText(f));
    }

    [Fact]
    public void WritesThroughASymlinkAndKeepsIt()
    {
        var real = dir.Write("real.json", "old");
        var link = dir.Symlink("link.json", "real.json");
        AtomicFile.Write(link, "new");
        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("new", File.ReadAllText(real));
    }

    [Fact]
    public void FollowsAChainToItsEnd()
    {
        var real = dir.Write("real", "old");
        dir.Symlink("l1", "l2");
        dir.Symlink("l2", real);
        dir.Symlink("l3", "l1");
        AtomicFile.Write(dir["l3"], "new");
        Assert.Equal("new", File.ReadAllText(real));
        Assert.Equal(real, AtomicFile.ResolveTarget(dir["l3"]));
        foreach (var link in new[] { "l1", "l2", "l3" })
        {
            Assert.NotNull(new FileInfo(dir[link]).LinkTarget);
        }
    }

    [Fact]
    public void FollowsALinkedDirectory()
    {
        var real = dir.Write("store/real/config.json", "old");
        dir.Symlink("alias", dir["store/real"]);
        AtomicFile.Write(dir["alias/config.json"], "new");
        Assert.Equal("new", File.ReadAllText(real));
        Assert.Equal(real, AtomicFile.ResolveTarget(dir["alias/config.json"]));
    }

    [Fact]
    public void CreatesTheTargetOfADanglingSymlink()
    {
        var link = dir.Symlink("link", "missing");
        AtomicFile.Write(link, "made");
        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("made", File.ReadAllText(dir["missing"]));
    }

    [Fact]
    public void NotFollowingReplacesTheLinkItself()
    {
        var real = dir.Write("real", "old");
        var link = dir.Symlink("link", real);
        AtomicFile.Write(link, "new", AtomicWriteOptions.Default with { FollowSymlinks = false });
        Assert.Null(new FileInfo(link).LinkTarget);
        Assert.Equal("old", File.ReadAllText(real));
        Assert.Equal("new", File.ReadAllText(link));
    }

    [Fact]
    public void ACycleIsRefusedAndTheLinksStay()
    {
        dir.Symlink("a", dir["b"]);
        dir.Symlink("b", dir["a"]);
        var err = Assert.Throws<IOException>(() => AtomicFile.Write(dir["a"], "v"));
        Assert.Contains("too many levels of symbolic links", err.Message, StringComparison.Ordinal);
        Assert.Equal(["a", "b"], dir.Entries());
        Assert.Equal(dir["b"], new FileInfo(dir["a"]).LinkTarget);
        Assert.Equal(dir["a"], new FileInfo(dir["b"]).LinkTarget);
    }

    [Fact]
    public void FortyLinksAreFollowedAndFortyOneRefused()
    {
        var real = dir.Write("real", "old");
        // link0 → link1 → … → link39 → real: forty hops.
        for (var i = 0; i < AtomicFile.MaxSymlinkHops; i++)
        {
            dir.Symlink($"link{i}", i == AtomicFile.MaxSymlinkHops - 1 ? "real" : $"link{i + 1}");
        }
        AtomicFile.Write(dir["link0"], "forty");
        Assert.Equal("forty", File.ReadAllText(real));

        dir.Symlink("extra", "link0");
        var err = Assert.Throws<IOException>(() => AtomicFile.Write(dir["extra"], "v"));
        Assert.Contains("too many levels", err.Message, StringComparison.Ordinal);
        Assert.Equal("forty", File.ReadAllText(real));
        Assert.Equal("link0", new FileInfo(dir["extra"]).LinkTarget);
        Assert.Equal(AtomicFile.MaxSymlinkHops + 2, dir.Entries().Length);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void KeepsTheOldMode()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix modes");
            return; // CA1416 does not see Assert.Skip as an exit.
        }
        var f = dir.Write("secret", "old");
        File.SetUnixFileMode(f, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        AtomicFile.Write(f, "new");
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(f));
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void ModeAppliesAtCreationAndYieldsToAnExistingFile()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix modes");
            return; // CA1416 does not see Assert.Skip as an exit.
        }
        const UnixFileMode Owner = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        const UnixFileMode Group = Owner | UnixFileMode.GroupRead;
        var f = dir["key"];
        var owner = AtomicWriteOptions.Default with { UnixCreateMode = Owner };
        AtomicFile.Write(f, "k", owner);
        Assert.Equal(Owner, File.GetUnixFileMode(f));
        File.SetUnixFileMode(f, Group);
        AtomicFile.Write(f, "k2", owner);
        Assert.Equal(Group, File.GetUnixFileMode(f));
        AtomicFile.Write(f, "k3", owner with { PreservePermissions = false });
        Assert.Equal(Owner, File.GetUnixFileMode(f));
    }

    [Fact]
    public void ReplacesAReadOnlyFile()
    {
        Assert.SkipUnless(
            OperatingSystem.IsWindows(),
            "the read-only bit blocks a rename only on Windows"
        );
        var f = dir.Write("ro.json", "old");
        new FileInfo(f).IsReadOnly = true;
        AtomicFile.Write(f, "new");
        Assert.Equal("new", File.ReadAllText(f));
    }
}
