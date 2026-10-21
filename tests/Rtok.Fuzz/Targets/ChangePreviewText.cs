using System.Globalization;
using System.Text.RegularExpressions;
using Pyrlyn.ChangePreview;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Two texts (split at the first NUL) through <see cref="UnifiedDiff"/>: each hunk's sides are
/// the texts' lines at the ranges its header names, the text between and after hunks is the same
/// in both, so the diff turns the old text into the new one; <see cref="Preview"/>'s totals count
/// the diff's <c>+</c> and <c>-</c> lines.
/// </summary>
internal static partial class ChangePreviewText
{
    private const string NoNewline = "\\ No newline at end of file";

    public static void Run(ReadOnlySpan<byte> data)
    {
        var parts = FuzzInput.Parts(data, 2);
        var (before, after) = (parts[0], parts[1]);
        var diff = UnifiedDiff.Render("f", before, after);
        FuzzInput.Check(
            diff.Length == 0 == string.Equals(before, after, StringComparison.Ordinal),
            "the diff is empty exactly when the texts are equal"
        );
        var old = Lines(before);
        var @new = Lines(after);
        var rows = diff.Split('\n');
        var (o, n) = (0, 0);
        for (var i = 2; i < rows.Length - 1; )
        {
            var m = Hunk().Match(rows[i]);
            FuzzInput.Check(m.Success, $"not a hunk header: {rows[i]}");
            var (oldAt, oldCount) = Range(m, "o");
            var (newAt, newCount) = Range(m, "n");
            FuzzInput.Check(
                oldAt - o == newAt - n && Same(old, o, @new, n, oldAt - o),
                "the text between hunks differs"
            );
            var (oldSide, newSide) = Sides(rows, ref i);
            FuzzInput.Check(
                oldSide.Count == oldCount && Same(old, oldAt, oldSide, 0, oldCount),
                "a hunk's old side is not the old text's lines"
            );
            FuzzInput.Check(
                newSide.Count == newCount && Same(@new, newAt, newSide, 0, newCount),
                "a hunk's new side is not the new text's lines"
            );
            (o, n) = (oldAt + oldCount, newAt + newCount);
        }
        FuzzInput.Check(
            old.Count - o == @new.Count - n && Same(old, o, @new, n, old.Count - o),
            "the text after the last hunk differs"
        );
        var totals = new Preview().Edit("f", before, after).Totals();
        var plus = rows.Skip(2).Count(r => r.StartsWith('+'));
        var minus = rows.Skip(2).Count(r => r.StartsWith('-'));
        FuzzInput.Check(
            totals.Insertions == plus && totals.Deletions == minus,
            "the totals do not count the diff's lines"
        );
    }

    /// <summary>The old and new lines of the hunk whose header is at <paramref name="i"/>; leaves <paramref name="i"/> on the next header.</summary>
    private static (List<string> Old, List<string> New) Sides(string[] rows, ref int i)
    {
        var (old, @new) = (new List<string>(), new List<string>());
        for (i++; i < rows.Length - 1 && !rows[i].StartsWith('@'); i++)
        {
            var last =
                i + 1 < rows.Length
                && string.Equals(rows[i + 1], NoNewline, StringComparison.Ordinal);
            var text = rows[i][1..] + (last ? "" : "\n");
            FuzzInput.Check(rows[i][0] is '-' or '+' or ' ', $"not a diff line: {rows[i]}");
            if (rows[i][0] != '+')
            {
                old.Add(text);
            }
            if (rows[i][0] != '-')
            {
                @new.Add(text);
            }
            i += last ? 1 : 0;
        }
        return (old, @new);
    }

    /// <summary>Lines with their <c>\n</c> kept, so a missing final newline is a different line.</summary>
    private static List<string> Lines(string text)
    {
        var lines = new List<string>();
        for (var start = 0; start < text.Length; )
        {
            var end = text.IndexOf('\n', start) is var nl and >= 0 ? nl + 1 : text.Length;
            lines.Add(text[start..end]);
            start = end;
        }
        return lines;
    }

    /// <summary>The 0-based first line and the count; <c>-a,0</c> inserts after line <c>a</c>.</summary>
    private static (int At, int Count) Range(Match m, string side)
    {
        var start = int.Parse(m.Groups[side].Value, CultureInfo.InvariantCulture);
        var count = m.Groups[side + "c"].Success
            ? int.Parse(m.Groups[side + "c"].Value, CultureInfo.InvariantCulture)
            : 1;
        return (count == 0 ? start : start - 1, count);
    }

    private static bool Same(List<string> a, int at, List<string> b, int bt, int count) =>
        count >= 0
        && at >= 0
        && bt >= 0
        && at + count <= a.Count
        && bt + count <= b.Count
        && a.GetRange(at, count).SequenceEqual(b.GetRange(bt, count), StringComparer.Ordinal);

    [GeneratedRegex(
        @"^@@ -(?<o>\d+)(?:,(?<oc>\d+))? \+(?<n>\d+)(?:,(?<nc>\d+))? @@$",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Hunk();
}
