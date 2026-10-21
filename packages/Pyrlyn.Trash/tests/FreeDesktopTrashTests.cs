using System.Runtime.Versioning;

namespace Pyrlyn.Trash.Tests;

/// <summary>The FreeDesktop spec, against a trash under a scratch <c>XDG_DATA_HOME</c>.</summary>
[SupportedOSPlatform("linux")]
public sealed class FreeDesktopTrashTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 9, 5, 7, DateTimeKind.Local);

    [Fact]
    public void TheInfoFileHoldsTheEscapedPathAndTheDate()
    {
        SkipOffLinux();
        using var scratch = new Scratch();
        var file = scratch.File("a b%.txt");

        var trashed = Move(scratch, file);

        Assert.Equal(Path.Join(scratch.Root, "data", "Trash", "files", "a b%.txt"), trashed);
        Assert.False(Path.Exists(file));
        Assert.Equal(
            $"[Trash Info]\nPath={scratch.Root}/a%20b%25.txt\nDeletionDate=2026-10-22T09:05:07\n",
            File.ReadAllText(Path.Join(scratch.Root, "data", "Trash", "info", "a b%.txt.trashinfo"))
        );
    }

    [Fact]
    public void ATakenNameGetsANumberBeforeTheExtension()
    {
        SkipOffLinux();
        using var scratch = new Scratch();

        var first = Move(scratch, scratch.File("one/a.txt"));
        var second = Move(scratch, scratch.File("two/a.txt"));
        var dotfile = Move(scratch, scratch.File("one/.env"));
        var dotfile2 = Move(scratch, scratch.File("two/.env"));

        Assert.Equal(
            ["a.txt", "a.2.txt", ".env", ".env.2"],
            new[] { first, second, dotfile, dotfile2 }.Select(Path.GetFileName),
            StringComparer.Ordinal
        );
        Assert.True(
            File.Exists(Path.Join(scratch.Root, "data", "Trash", "info", "a.2.txt.trashinfo"))
        );
    }

    [Fact]
    public void AFailedMoveLeavesTheItemAndNoInfoFile()
    {
        SkipOffLinux();
        using var scratch = new Scratch();
        var file = scratch.File("keep.txt", "here");
        Directory.CreateDirectory(Path.Join(scratch.Root, "data", "Trash"));
        File.WriteAllText(Path.Join(scratch.Root, "data", "Trash", "files"), "not a folder");

        Assert.Throws<TrashException>(() => Move(scratch, file));

        Assert.Equal("here", File.ReadAllText(file));
        Assert.False(
            Directory.Exists(Path.Join(scratch.Root, "data", "Trash", "info"))
                && Directory
                    .EnumerateFileSystemEntries(Path.Join(scratch.Root, "data", "Trash", "info"))
                    .Any()
        );
    }

    [Fact]
    public void TheTrashItselfIsNotTrashed()
    {
        SkipOffLinux();
        using var scratch = new Scratch();
        var inside = scratch.File("data/Trash/files/old.txt");

        Assert.Throws<TrashException>(() => Move(scratch, inside));
        Assert.True(File.Exists(inside));
    }

    [Fact]
    public void AStickyAdminTrashGetsAFolderPerUser()
    {
        SkipOffLinux();
        using var scratch = new Scratch();
        var admin = scratch.Dir(".Trash");
        File.SetUnixFileMode(admin, (UnixFileMode)Convert.ToInt32("1777", 8));

        var trash = TopTrash(scratch.Root);

        Assert.Equal(admin, Path.GetDirectoryName(trash));
        Assert.Matches("^[0-9]+$", Path.GetFileName(trash));
    }

    [Fact]
    public void AnAdminTrashWithoutTheStickyBitIsSkipped()
    {
        SkipOffLinux();
        using var scratch = new Scratch();
        scratch.Dir(".Trash");

        Assert.Matches(@"/\.Trash-[0-9]+$", TopTrash(scratch.Root));
    }

    [Fact]
    public void ASymlinkedAdminTrashIsSkipped()
    {
        SkipOffLinux();
        using var scratch = new Scratch();
        var elsewhere = scratch.Dir("elsewhere");
        File.SetUnixFileMode(elsewhere, (UnixFileMode)Convert.ToInt32("1777", 8));
        Directory.CreateSymbolicLink(Path.Join(scratch.Root, ".Trash"), elsewhere);

        Assert.Matches(@"/\.Trash-[0-9]+$", TopTrash(scratch.Root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
    }

    /// <summary>Needs a folder on another device than the home trash, e.g. a tmpfs: <c>PYRLYN_TRASH_OTHER_DEVICE=/mnt/t</c>.</summary>
    [Fact]
    public void AnItemOnAnotherDeviceGoesToTheTrashAtTheTopOfItsMount()
    {
        SkipOffLinux();
        var mount = Environment.GetEnvironmentVariable("PYRLYN_TRASH_OTHER_DEVICE");
        if (string.IsNullOrEmpty(mount))
        {
            Assert.Skip("set PYRLYN_TRASH_OTHER_DEVICE to the top of another mount");
        }
        var dir = Directory
            .CreateDirectory(Path.Join(mount, "pyrlyn-" + Guid.NewGuid().ToString("N")))
            .FullName;
        var file = Path.Join(dir, "far.txt");
        File.WriteAllText(file, "far");

        var trashed = OsTrash.Move(file);

        Assert.False(File.Exists(file));
        Assert.Matches(@"^" + mount + @"/\.Trash-[0-9]+/files/far\.txt$", trashed);
        var info = File.ReadAllText(
            Path.Join(
                Path.GetDirectoryName(Path.GetDirectoryName(trashed)),
                "info",
                "far.txt.trashinfo"
            )
        );
        Assert.Contains(
            $"\nPath={Path.GetFileName(dir)}/far.txt\n",
            info,
            StringComparison.Ordinal
        );
    }

    private static void SkipOffLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("the FreeDesktop trash is Linux's");
        }
    }

    private static string Move(Scratch scratch, string path) =>
        FreeDesktopTrash.Move(path, Path.Join(scratch.Root, "data"), Now);

    private static string TopTrash(string top) => FreeDesktopTrash.TopTrash(top);
}
