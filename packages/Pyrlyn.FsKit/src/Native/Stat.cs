using System.Runtime.InteropServices;

namespace Pyrlyn.FsKit.Native;

/// <summary>What <c>lstat</c> (or its Windows equivalent) says about one entry, never following it.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct Stat(
    ulong Device,
    ulong FileId,
    ulong Links,
    StatKind Kind,
    long Size,
    long Allocated
);
