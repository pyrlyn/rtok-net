using Pyrlyn.FsKit;

namespace Pyrlyn.Trash;

/// <summary>
/// Moves a file, folder or symlink to the trash the OS shows: the macOS Trash, the FreeDesktop
/// trash on Linux, the Windows Recycle Bin. The item is moved, never copied and deleted, and a
/// failure leaves it in place.
/// </summary>
public static class OsTrash
{
    /// <summary>True on macOS, Linux and Windows.</summary>
    public static bool IsSupported =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    /// <summary>Moves <paramref name="path"/> to the trash. A symlink is trashed itself, not its target.</summary>
    /// <param name="path">The item to trash.</param>
    /// <returns>Where the item is now, or null when the OS does not say.</returns>
    /// <exception cref="FileNotFoundException">Nothing is at <paramref name="path"/>.</exception>
    /// <exception cref="TrashException">The OS refused; the item is still at <paramref name="path"/>.</exception>
    /// <exception cref="PlatformNotSupportedException">Not macOS, Linux or Windows.</exception>
    public static string? Move(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        _ = FileIdentity.Of(full, followLinks: false);
        if (OperatingSystem.IsMacOS())
        {
            return MacTrash.Move(full);
        }
        if (OperatingSystem.IsLinux())
        {
            return FreeDesktopTrash.Move(full, FreeDesktopTrash.DataHome(), DateTime.Now);
        }
        if (OperatingSystem.IsWindows())
        {
            return WindowsTrash.Move(full);
        }
        throw new PlatformNotSupportedException("no trash on this OS");
    }
}
