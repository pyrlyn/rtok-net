using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Tags;

/// <summary>A half-open range <c>[Start, End)</c> of bytes or columns.</summary>
/// <param name="Start">First position.</param>
/// <param name="End">One past the last position.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TextRange(int Start, int End)
{
    /// <summary><c>End - Start</c>.</summary>
    public int Length => End - Start;
}
