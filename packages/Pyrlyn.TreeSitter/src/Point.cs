using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter;

/// <summary>A position in source: zero-based row and zero-based byte column.</summary>
/// <param name="Row">Zero-based line.</param>
/// <param name="Column">Zero-based byte offset within the line.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Point(int Row, int Column);
