using System.Runtime.InteropServices;
using Pyrlyn.FsKit.Native;

namespace Pyrlyn.FsKit;

/// <summary>
/// Which file an entry is, whatever path reaches it: device and inode on Unix, volume serial and
/// file index on Windows. Two hard links to one file have the same identity.
/// </summary>
/// <param name="Device">The device (Unix) or volume serial number (Windows).</param>
/// <param name="FileId">The inode (Unix) or file index (Windows).</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FileIdentity(ulong Device, ulong FileId)
{
    /// <summary>The identity of what <paramref name="path"/> names.</summary>
    /// <param name="path">The entry.</param>
    /// <param name="followLinks">When false, a symlink's own identity rather than its target's.</param>
    /// <exception cref="FileNotFoundException">Nothing is at <paramref name="path"/>.</exception>
    /// <exception cref="PlatformNotSupportedException">FsKit has no stat layout for this platform.</exception>
    public static FileIdentity Of(string path, bool followLinks = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var at = followLinks ? FsPath.Canonicalize(path) : path;
        if (!Fs.TryLStat(at, out var stat, out var error))
        {
            throw error == 0 ? new PlatformNotSupportedException() : Fs.Exception(error, path);
        }
        return new FileIdentity(stat.Device, stat.FileId);
    }

    /// <summary><see cref="Of"/> that answers false instead of throwing.</summary>
    public static bool TryOf(string path, out FileIdentity identity, bool followLinks = true)
    {
        identity = default;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        string? at = path;
        if (followLinks && !FsPath.TryCanonicalize(path, out at))
        {
            return false;
        }
        if (!Fs.TryLStat(at, out var stat, out _))
        {
            return false;
        }
        identity = new FileIdentity(stat.Device, stat.FileId);
        return true;
    }

    /// <summary>Whether two paths reach one file, hard links included; false when either is missing.</summary>
    public static bool Same(string a, string b) =>
        TryOf(a, out var x) && TryOf(b, out var y) && x == y;
}
