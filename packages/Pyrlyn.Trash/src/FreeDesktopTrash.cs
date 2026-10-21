using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Pyrlyn.FsKit;

namespace Pyrlyn.Trash;

/// <summary>
/// The FreeDesktop trash spec (1.0): the home trash for items on its device, otherwise the
/// trash at the top of the item's mount. The <c>.trashinfo</c> file is created first, with
/// <c>CreateNew</c>, to claim the name; then the item is renamed into <c>files/</c>. An item on
/// another device is never copied.
/// </summary>
[SupportedOSPlatform("linux")]
internal static class FreeDesktopTrash
{
    private const UnixFileMode Private =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary><c>$XDG_DATA_HOME</c>, or <c>~/.local/share</c> when it is unset or relative.</summary>
    public static string DataHome()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrEmpty(xdg) && Path.IsPathFullyQualified(xdg)
            ? xdg
            : Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share"
            );
    }

    /// <summary>Moves the item to the home trash or its mount's trash.</summary>
    /// <param name="full">An absolute path with no trailing separator.</param>
    /// <param name="dataHome">Where the home trash lives (<c>Trash</c> under it).</param>
    /// <param name="now">The deletion date, local time.</param>
    public static string Move(string full, string dataHome, DateTime now)
    {
        // Resolve the folder, not the item: a symlink is trashed itself.
        var item = Path.Join(
            FsPath.Canonicalize(Path.GetDirectoryName(full) ?? "/"),
            Path.GetFileName(full)
        );
        var device = FileIdentity.Of(item, followLinks: false).Device;
        var home = Path.Join(dataHome, "Trash");
        if (FsPath.StartsWith(item, home) || FsPath.SamePath(item, dataHome))
        {
            throw new TrashException($"{item}: is the trash or inside it");
        }
        if (DeviceOf(home) == device)
        {
            return Into(home, item, item, now);
        }
        var top = TopDir(item, device);
        return Into(TopTrash(top), item, Path.GetRelativePath(top, item), now);
    }

    /// <summary>The trash at the top of a mount: <c>.Trash/$uid</c> when an admin made <c>.Trash</c> right, else <c>.Trash-$uid</c>.</summary>
    internal static string TopTrash(string top)
    {
        var uid = Uid();
        var admin = Path.Join(top, ".Trash");
        var adminInfo = new DirectoryInfo(admin);
        if (
            adminInfo.Exists
            && adminInfo.LinkTarget is null
            && File.GetUnixFileMode(admin).HasFlag(UnixFileMode.StickyBit)
        )
        {
            var mine = Path.Join(admin, uid);
            if (TryOwnDirectory(mine))
            {
                return mine;
            }
        }
        var own = Path.Join(top, $".Trash-{uid}");
        return TryOwnDirectory(own)
            ? own
            : throw new TrashException(
                $"{top}: no usable trash folder (.Trash/{uid} or .Trash-{uid})"
            );
    }

    private static bool TryOwnDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path, Private);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        return new DirectoryInfo(path).LinkTarget is null;
    }

    private static string Into(string trash, string item, string infoPath, DateTime now)
    {
        var files = Path.Join(trash, "files");
        var info = Path.Join(trash, "info");
        try
        {
            Directory.CreateDirectory(files, Private);
            Directory.CreateDirectory(info, Private);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new TrashException($"{trash}: {e.Message}", e);
        }
        var (name, infoFile) = Claim(files, info, Path.GetFileName(item), infoPath, now);
        var target = Path.Join(files, name);
        try
        {
            // rename(2) for files, folders and symlinks alike. File.Move links and then unlinks,
            // which leaves a second link in the trash when the unlink is refused.
            Directory.Move(item, target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            File.Delete(infoFile);
            throw new TrashException($"{item}: {e.Message}", e);
        }
        return target;
    }

    /// <summary>The first free name (<c>a.txt</c>, <c>a.2.txt</c>, ...), claimed by writing its <c>.trashinfo</c>.</summary>
    private static (string Name, string InfoFile) Claim(
        string files,
        string info,
        string name,
        string infoPath,
        DateTime now
    )
    {
        var body = Encoding.UTF8.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"[Trash Info]\nPath={Escape(infoPath)}\nDeletionDate={now:yyyy-MM-ddTHH:mm:ss}\n"
            )
        );
        for (var n = 1; n < 10_000; n++)
        {
            var candidate = n == 1 ? name : Numbered(name, n);
            var infoFile = Path.Join(info, candidate + ".trashinfo");
            try
            {
                using (var stream = new FileStream(infoFile, FileMode.CreateNew, FileAccess.Write))
                {
                    stream.Write(body);
                }
            }
            catch (IOException) when (File.Exists(infoFile))
            {
                continue;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new TrashException($"{info}: {e.Message}", e);
            }
            if (!FileIdentity.TryOf(Path.Join(files, candidate), out _, followLinks: false))
            {
                return (candidate, infoFile);
            }
            File.Delete(infoFile); // a leftover in files/ without its info file
        }
        throw new TrashException($"{files}: no free name for {name}");
    }

    private static string Numbered(string name, int n)
    {
        var ext = Path.GetExtension(name);
        var stem = name[..^ext.Length];
        return stem.Length == 0 ? $"{name}.{n}" : $"{stem}.{n}{ext}";
    }

    /// <summary>Percent-encodes each segment; the separators stay.</summary>
    private static string Escape(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    /// <summary>The device of the path, or of its nearest existing parent.</summary>
    private static ulong? DeviceOf(string path)
    {
        for (var at = path; at is not null; at = Path.GetDirectoryName(at))
        {
            if (FileIdentity.TryOf(at, out var id))
            {
                return id.Device;
            }
        }
        return null;
    }

    /// <summary>The highest folder above the item still on its device: the mount point.</summary>
    private static string TopDir(string item, ulong device)
    {
        var top = Path.GetDirectoryName(item) ?? "/";
        for (var up = Path.GetDirectoryName(top); up is not null; up = Path.GetDirectoryName(up))
        {
            if (!FileIdentity.TryOf(up, out var id) || id.Device != device)
            {
                break;
            }
            top = up;
        }
        return top;
    }

    /// <summary>The effective uid, from <c>/proc/self/status</c>: no libc call needed.</summary>
    private static string Uid()
    {
        foreach (var line in File.ReadLines("/proc/self/status"))
        {
            if (line.StartsWith("Uid:", StringComparison.Ordinal))
            {
                return line.Split('\t', StringSplitOptions.RemoveEmptyEntries)[2];
            }
        }
        throw new TrashException("no Uid line in /proc/self/status");
    }
}
