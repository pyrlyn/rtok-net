using System.Text;

namespace Rtok.Agents.Usage;

/// <summary>
/// The padded-column table: every column as wide as its widest cell, columns separated by one
/// space, one line per row; the first row is the header. Numbers right-align, text left-aligns.
/// End a table on a right-aligned column, or its last column keeps its trailing pad.
/// <paramref name="floors"/>, when given, are the columns' minimum widths.
/// </summary>
internal static class TextTable
{
    public static string Render(
        IReadOnlyList<bool> rightAligned,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<int>? floors = null
    )
    {
        var widths = new int[rightAligned.Count];
        for (var j = 0; floors is not null && j < widths.Length && j < floors.Count; j++)
        {
            widths[j] = floors[j];
        }
        foreach (var row in rows)
        {
            for (var j = 0; j < widths.Length && j < row.Count; j++)
            {
                widths[j] = Math.Max(widths[j], Width(row[j]));
            }
        }
        var out_ = new StringBuilder();
        foreach (var row in rows)
        {
            for (var j = 0; j < widths.Length; j++)
            {
                var cell = j < row.Count ? row[j] : "";
                var pad = new string(' ', widths[j] - Width(cell));
                out_.Append(j > 0 ? " " : "").Append(rightAligned[j] ? pad + cell : cell + pad);
            }
            out_.Append('\n');
        }
        return out_.ToString();
    }

    /// <summary>The cell's width in Unicode scalar values.</summary>
    private static int Width(string cell) => cell.EnumerateRunes().Count();
}
