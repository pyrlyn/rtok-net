using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsCheck;

namespace Pyrlyn.ChangePreview.Tests;

/// <summary>Any diff, applied to the old text the way <c>patch</c> does, gives the new text.</summary>
public sealed partial class UnifiedDiffPropertyTests
{
    // Few distinct lines, so the diffs have kept lines, runs and missing final newlines.
    private static readonly Gen<string> Text = Gen.String[Gen.Char["ab\n"], 0, 40];

    [Fact]
    public void ApplyingTheDiffToTheOldTextGivesTheNewText() =>
        Gen.Select(Text, Text)
            .Sample(
                (before, after) =>
                    Assert.Equal(after, Apply(before, UnifiedDiff.Render("f", before, after)))
            );

    [Fact]
    public void EditCountsMatchTheDiffLines() =>
        Gen.Select(Text, Text)
            .Sample(
                (before, after) =>
                {
                    var p = new Preview().Edit("f", before, after);
                    var diff = UnifiedDiff.Render("f", before, after).Split('\n').Skip(2).ToList();
                    var (ins, del) = p.IsEmpty
                        ? (0, 0)
                        : (p.Edits[0].Insertions, p.Edits[0].Deletions);
                    Assert.Equal(diff.Count(l => l.StartsWith('+')), ins);
                    Assert.Equal(diff.Count(l => l.StartsWith('-')), del);
                }
            );

    private static string Apply(string before, string diff)
    {
        var old = LineChunker.Instance.Chunk(before);
        var lines = diff.Split('\n');
        var output = new StringBuilder();
        var next = 0; // the next old line not yet copied
        for (var i = 2; i < lines.Length - 1; i++)
        {
            var m = Hunk().Match(lines[i]);
            Assert.True(m.Success, lines[i]);
            var start = int.Parse(m.Groups["start"].Value, CultureInfo.InvariantCulture);
            var count = m.Groups["count"].Success
                ? int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture)
                : 1;
            var first = count == 0 ? start : start - 1;
            for (; next < first; next++)
            {
                output.Append(old[next]);
            }
            for (i++; i < lines.Length - 1 && !lines[i].StartsWith('@'); i++)
            {
                var noNewline =
                    i + 1 < lines.Length
                    && string.Equals(
                        lines[i + 1],
                        "\\ No newline at end of file",
                        StringComparison.Ordinal
                    );
                var text = lines[i][1..] + (noNewline ? "" : "\n");
                if (lines[i][0] == '-' || lines[i][0] == ' ')
                {
                    Assert.Equal(old[next], text);
                    next++;
                }
                if (lines[i][0] == '+' || lines[i][0] == ' ')
                {
                    output.Append(text);
                }
                i += noNewline ? 1 : 0;
            }
            i--;
        }
        for (; next < old.Count; next++)
        {
            output.Append(old[next]);
        }
        return output.ToString();
    }

    [GeneratedRegex(
        @"^@@ -(?<start>\d+)(?:,(?<count>\d+))? \+\d+(?:,\d+)? @@$",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Hunk();
}
