namespace Pyrlyn.Trash.Tests;

/// <summary>The real trash of the OS the tests run on; each test removes what it put there.</summary>
public sealed class OsTrashTests
{
    [Fact]
    public void AFileLeavesItsPathAndIsInTheTrash()
    {
        using var scratch = new Scratch();
        var file = scratch.File("note.txt", "kept");

        var trashed = OsTrash.Move(file);

        try
        {
            Assert.False(Path.Exists(file));
            Assert.NotNull(trashed);
            Assert.Equal("kept", File.ReadAllText(trashed));
        }
        finally
        {
            Remove(trashed);
        }
    }

    [Fact]
    public void AFolderGoesWithEverythingInIt()
    {
        using var scratch = new Scratch();
        scratch.File("dir/a/b.txt", "deep");
        var dir = Path.Join(scratch.Root, "dir");

        var trashed = OsTrash.Move(dir + Path.DirectorySeparatorChar);

        try
        {
            Assert.False(Path.Exists(dir));
            Assert.NotNull(trashed);
            Assert.Equal("deep", File.ReadAllText(Path.Join(trashed, "a", "b.txt")));
        }
        finally
        {
            Remove(trashed);
        }
    }

    [Fact]
    public void ASymlinkIsTrashedNotItsTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("creating symlinks needs developer mode on Windows");
        }
        using var scratch = new Scratch();
        var target = scratch.File("target.txt", "stays");
        var link = Path.Join(scratch.Root, "link");
        File.CreateSymbolicLink(link, target);

        var trashed = OsTrash.Move(link);

        try
        {
            Assert.False(Path.Exists(link));
            Assert.Equal("stays", File.ReadAllText(target));
            Assert.NotNull(trashed);
            Assert.NotNull(new FileInfo(trashed).LinkTarget);
        }
        finally
        {
            Remove(trashed);
        }
    }

    [Fact]
    public void NothingAtThePathThrowsFileNotFound()
    {
        using var scratch = new Scratch();

        Assert.Throws<FileNotFoundException>(() =>
            OsTrash.Move(Path.Join(scratch.Root, "missing"))
        );
    }

    [Fact]
    public void ARefusedMoveLeavesTheItemInPlace()
    {
        using var scratch = new Scratch();
        var file = scratch.File("locked/held.txt", "here");
        Action undo;
        if (OperatingSystem.IsWindows())
        {
            var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            undo = held.Dispose;
        }
        else if (string.Equals(Environment.UserName, "root", StringComparison.Ordinal))
        {
            Assert.Skip(
                "root ignores folder permissions; FreeDesktopTrashTests cover the Linux failure path"
            );
            return;
        }
        else
        {
            // The item cannot leave a folder nobody may write to.
            File.SetUnixFileMode(
                Path.GetDirectoryName(file)!,
                UnixFileMode.UserRead | UnixFileMode.UserExecute
            );
            undo = () => { };
        }

        try
        {
            var before = Trashed();

            Assert.Throws<TrashException>(() => OsTrash.Move(file));
            Assert.Equal("here", File.ReadAllText(file));
            Assert.Equal(before, Trashed());
        }
        finally
        {
            undo();
        }
    }

    /// <summary>What the Linux home trash holds; elsewhere the trash is not listed (macOS guards <c>~/.Trash</c>).</summary>
    private static string[] Trashed()
    {
        var files = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "Trash",
            "files"
        );
        return OperatingSystem.IsLinux() && Directory.Exists(files)
            ? [.. Directory.EnumerateFileSystemEntries(files).Order(StringComparer.Ordinal)]
            : [];
    }

    private static void Remove(string? trashed)
    {
        if (trashed is null)
        {
            return;
        }
        if (Directory.Exists(trashed) && new DirectoryInfo(trashed).LinkTarget is null)
        {
            Directory.Delete(trashed, recursive: true);
        }
        else
        {
            File.Delete(trashed);
        }
        // The record of where it came from: info/<name>.trashinfo on Linux, $I<id> beside $R<id> on Windows.
        var name = Path.GetFileName(trashed);
        var folder = Path.GetDirectoryName(trashed)!;
        if (OperatingSystem.IsLinux())
        {
            File.Delete(Path.Join(Path.GetDirectoryName(folder), "info", name + ".trashinfo"));
        }
        else if (OperatingSystem.IsWindows() && name.StartsWith("$R", StringComparison.Ordinal))
        {
            File.Delete(Path.Join(folder, "$I" + name[2..]));
        }
    }
}
