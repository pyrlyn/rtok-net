using DiffPlex;

namespace Pyrlyn.ChangePreview;

/// <summary>
/// Lines with their line ends kept, so <c>x</c> and <c>x\n</c> differ and a missing final
/// newline shows in the diff. DiffPlex's own line chunker drops the ends.
/// </summary>
internal sealed class LineChunker : IChunker
{
    internal static readonly LineChunker Instance = new();

    public IReadOnlyList<string> Chunk(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline + 1;
            lines.Add(text[start..end]);
            start = end;
        }
        return lines;
    }
}
