using System.Runtime.InteropServices;

namespace Pyrlyn.FsKit;

/// <summary>
/// Which file an entry is: its device (a Windows volume serial) and its inode (a Windows file
/// index). It survives a rename, and a new file at the same path has another one.
/// </summary>
/// <param name="Device">The device or volume.</param>
/// <param name="Id">The inode or file index on that device.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FileId(ulong Device, ulong Id);
