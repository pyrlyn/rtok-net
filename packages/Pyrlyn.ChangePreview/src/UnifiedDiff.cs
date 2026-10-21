using System.Globalization;
using System.Text;
using DiffPlex;

namespace Pyrlyn.ChangePreview;

/// <summary>The unified diff <c>git diff</c> prints, from DiffPlex's line diff.</summary>
public static class UnifiedDiff
{
    private const int Context = 3;

    /// <summary>The diff of one file with <c>a/</c> and <c>b/</c> headers and three lines of context; empty when nothing differs.</summary>
    /// <param name="path">The path in the headers.</param>
    /// <param name="before">The text now.</param>
    /// <param name="after">The text the command would write.</param>
    /// <returns>The diff, without colour.</returns>
    public static string Render(string path, string before, string after)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return string.Equals(before, after, StringComparison.Ordinal)
            ? ""
            : Format(path, Lines(before, after));
    }

    /// <summary>Every line of both sides in order: kept, removed (<c>-</c>) or added (<c>+</c>).</summary>
    internal static List<DiffLine> Lines(string before, string after)
    {
        var result = Differ.Instance.CreateDiffs(before, after, false, false, LineChunker.Instance);
        var lines = new List<DiffLine>();
        var (a, b) = (0, 0);
        void Keep(int until)
        {
            for (; a < until; a++, b++)
            {
                lines.Add(new DiffLine(' ', result.PiecesOld[a], a, b));
            }
        }
        foreach (var block in result.DiffBlocks)
        {
            Keep(block.DeleteStartA);
            for (var i = 0; i < block.DeleteCountA; i++, a++)
            {
                lines.Add(new DiffLine('-', result.PiecesOld[a], a, b));
            }
            for (var i = 0; i < block.InsertCountB; i++, b++)
            {
                lines.Add(new DiffLine('+', result.PiecesNew[b], a, b));
            }
        }
        Keep(result.PiecesOld.Count);
        return lines;
    }

    internal static string Format(string path, List<DiffLine> lines)
    {
        var sb = new StringBuilder();
        sb.Append("--- a/").Append(path).Append('\n');
        sb.Append("+++ b/").Append(path).Append('\n');
        foreach (var (first, last) in Hunks(lines))
        {
            var hunk = lines[first..(last + 1)];
            var oldCount = hunk.Count(l => l.Kind != '+');
            var newCount = hunk.Count(l => l.Kind != '-');
            sb.Append("@@ -")
                .Append(Range(hunk[0].Old, oldCount))
                .Append(" +")
                .Append(Range(hunk[0].New, newCount))
                .Append(" @@\n");
            foreach (var line in hunk)
            {
                sb.Append(line.Kind).Append(line.Text);
                if (!line.Text.EndsWith('\n'))
                {
                    sb.Append("\n\\ No newline at end of file\n");
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// The first and last line of every hunk: each change with up to three kept lines around
    /// it, and changes closer than seven kept lines share a hunk.
    /// </summary>
    private static IEnumerable<(int First, int Last)> Hunks(List<DiffLine> lines)
    {
        var changes = Enumerable.Range(0, lines.Count).Where(i => lines[i].Kind != ' ').ToList();
        var start = 0;
        for (var i = 1; i <= changes.Count; i++)
        {
            if (i < changes.Count && changes[i] - changes[i - 1] - 1 <= 2 * Context)
            {
                continue;
            }
            yield return (
                Math.Max(0, changes[start] - Context),
                Math.Min(lines.Count - 1, changes[i - 1] + Context)
            );
            start = i;
        }
    }

    /// <summary><c>3</c> for one line, <c>3,4</c> for more, and an empty range starts at the line before it.</summary>
    private static string Range(int index, int count) =>
        count == 1
            ? (index + 1).ToString(CultureInfo.InvariantCulture)
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{(count == 0 ? index : index + 1)},{count}"
            );
}
