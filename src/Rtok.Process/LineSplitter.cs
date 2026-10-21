using System.Text;

namespace Rtok.Process;

/// <summary>
/// Splits bytes into lines on <c>\n</c> and <c>\r</c>: git, curl and cargo redraw a progress
/// line with a bare <c>\r</c>, so a newline-only split would deliver a whole transfer as one line.
/// </summary>
internal sealed class LineSplitter
{
    /// <summary>A line longer than this is a binary blob, not progress; the rest of it is dropped.</summary>
    internal const int LineCap = 8192;

    private readonly List<byte> line = [];

    public void Push(ReadOnlySpan<byte> chunk, Action<string> each)
    {
        foreach (var b in chunk)
        {
            if (b is (byte)'\n' or (byte)'\r')
            {
                Finish(each);
            }
            else if (line.Count < LineCap)
            {
                line.Add(b);
            }
        }
    }

    /// <summary>Hands over the unterminated tail at the end of the stream.</summary>
    public void Finish(Action<string> each)
    {
        if (line.Count > 0)
        {
            each(Encoding.UTF8.GetString([.. line]));
            line.Clear();
        }
    }
}
