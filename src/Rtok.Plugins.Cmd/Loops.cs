using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// <c>for</c>/<c>while</c>/<c>until</c> output (rtok T678): a polling loop prints the same line
/// again and again. Each line is cut at its numbers and ids, and runs of three or more lines
/// with the same cut collapse to <c>shape ×N (vals: a..b)</c>. A line with an error word is
/// never collapsed and ends a run, so the one failing poll among fifty healthy ones stays
/// verbatim; the values between are in the archive.
/// </summary>
internal static partial class Loops
{
    // Fewer lines than this are not worth a `×N` line.
    private const int MinRun = 3;

    /// <summary><paramref name="output"/> with runs of same-shape lines collapsed, or null when
    /// nothing collapsed.</summary>
    public static string? Collapse(string output)
    {
        var lines = Text.Lines(output);
        var cuts = lines.ConvertAll(Cut.Of);
        var result = new List<string>();
        var changed = false;
        var i = 0;
        while (i < lines.Count)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || Problem().IsMatch(lines[i]))
            {
                result.Add(lines[i++]);
                continue;
            }

            // Blank lines (`echo` between polls) do not end a run, but only the ones inside
            // it are dropped.
            var members = new List<int> { i };
            var j = i + 1;
            while (j < lines.Count)
            {
                if (string.IsNullOrWhiteSpace(lines[j]))
                {
                    j++;
                }
                else if (!Problem().IsMatch(lines[j]) && cuts[j].SameShape(cuts[i]))
                {
                    members.Add(j++);
                }
                else
                {
                    break;
                }
            }

            if (members.Count < MinRun)
            {
                result.Add(lines[i++]);
                continue;
            }

            result.Add(RunLine(cuts, members));
            changed = true;
            i = members[^1] + 1;
        }

        if (!changed)
        {
            return null;
        }

        var got = string.Join('\n', result);
        return Text.Bytes(got) < Text.Bytes(output) ? got : null;
    }

    // `a..b` for a field that changes: the smallest and largest when every value is a number,
    // else the first and last (ids carry no order).
    private static string Span(List<string> vals)
    {
        var nums = new List<double>(vals.Count);
        foreach (var v in vals)
        {
            if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            {
                return $"{vals[0]}..{vals[^1]}";
            }

            nums.Add(n);
        }

        return $"{vals[nums.IndexOf(nums.Min())]}..{vals[nums.IndexOf(nums.Max())]}";
    }

    // One line for the run: constant fields written back in, changing ones as `#` with their
    // value range after `×N`.
    private static string RunLine(List<Cut> cuts, List<int> members)
    {
        var first = cuts[members[0]];
        var line = new StringBuilder();
        var vals = new List<string>();
        for (var k = 0; k < first.Parts.Count; k++)
        {
            line.Append(first.Parts[k]);
            if (k >= first.Vals.Count)
            {
                continue;
            }

            if (
                members.TrueForAll(m =>
                    string.Equals(cuts[m].Vals[k], first.Vals[k], StringComparison.Ordinal)
                )
            )
            {
                line.Append(first.Vals[k]);
            }
            else
            {
                line.Append('#');
                vals.Add(Span(members.ConvertAll(m => cuts[m].Vals[k])));
            }
        }

        return vals.Count == 0
            ? $"{line} ×{members.Count}"
            : $"{line} ×{members.Count} (vals: {string.Join(", ", vals)})";
    }

    [GeneratedRegex(
        @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|\b[0-9a-f]{12,}\b|\d+(?:\.\d+)?",
        RegexOptions.None,
        1000
    )]
    private static partial Regex Field();

    [GeneratedRegex(
        @"error|fail|warn|panic|exception|denied|refused|timed? ?out|fatal|unreachable",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        1000
    )]
    private static partial Regex Problem();

    // A line cut at its number/id fields: the text between them and the fields themselves.
    private sealed record Cut(List<string> Parts, List<string> Vals)
    {
        public static Cut Of(string line)
        {
            var parts = new List<string>();
            var vals = new List<string>();
            var at = 0;
            foreach (Match m in Field().Matches(line))
            {
                parts.Add(line[at..m.Index]);
                vals.Add(m.Value);
                at = m.Index + m.Length;
            }

            parts.Add(line[at..]);
            return new(parts, vals);
        }

        public bool SameShape(Cut other) =>
            Parts.SequenceEqual(other.Parts, StringComparer.Ordinal);
    }
}
