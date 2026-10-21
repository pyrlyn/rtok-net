namespace Pyrlyn.FsKit.Native;

/// <summary>The stat calls for this platform, or none where FsKit has no layout for it.</summary>
internal static class Fs
{
    public static bool TryLStat(string path, out Stat stat, out int error)
    {
        if (OperatingSystem.IsWindows())
        {
            return Win32.TryLStat(path, out stat, out error);
        }
        if (Unix.Supported)
        {
            return Unix.TryLStat(path, out stat, out error);
        }
        stat = default;
        error = 0;
        return false;
    }

    /// <summary>realpath(3) is POSIX, so it needs no per-platform layout.</summary>
    public static string? TryRealPath(string path, out int error) =>
        OperatingSystem.IsWindows()
            ? Win32.TryFinalPath(path, out error)
            : Unix.TryRealPath(path, out error);

    public static bool IsDenied(int error) =>
        OperatingSystem.IsWindows() ? error == 5 : error is Unix.EACCES or Unix.EPERM;

    /// <summary>The exception .NET would throw for <paramref name="error"/> at <paramref name="path"/>.</summary>
    public static Exception Exception(int error, string path) =>
        OperatingSystem.IsWindows() ? Win32.Exception(error, path) : Unix.Exception(error, path);
}
