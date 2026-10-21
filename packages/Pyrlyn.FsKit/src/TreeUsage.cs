using System.Runtime.InteropServices;

namespace Pyrlyn.FsKit;

/// <summary>What one walk found. A folder it could not read or finish makes it a lower bound, never an error.</summary>
/// <param name="Bytes">Allocated bytes: what removing the tree frees.</param>
/// <param name="Files">Every entry that is not a directory, symlinks included.</param>
/// <param name="Denied">Some entry could not be read.</param>
/// <param name="TimedOut">The walk stopped at its time limit.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TreeUsage(long Bytes, long Files, bool Denied, bool TimedOut);
