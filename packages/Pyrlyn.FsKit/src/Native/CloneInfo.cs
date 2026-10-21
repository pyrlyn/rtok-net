using System.Runtime.InteropServices;

namespace Pyrlyn.FsKit.Native;

/// <summary>An APFS clone family: files sharing <see cref="CloneId"/> share blocks.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct CloneInfo(ulong CloneId, uint RefCount, long PrivateSize);
