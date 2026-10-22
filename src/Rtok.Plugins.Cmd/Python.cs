using System.Text.RegularExpressions;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// Python output (rtok T675): <c>pytest</c> / <c>unittest</c> sessions and tracebacks. A passing
/// run shrinks to its summary; a failing run keeps every failure block, the short summary and the
/// totals; progress lines survive only when they show a failure. A traceback keeps all its frames,
/// but a run of identical frames (deep recursion) folds into one
/// <c>[previous frame repeated N more times]</c> line. Every function returns null when the text
/// is not the shape it knows.
/// </summary>
internal static partial class Python
{
    private static readonly string[] Problems = ["ERROR", "WARN", "Error", "error"];

    /// <summary>A pytest session: header and passing progress dropped; failures, errors,
    /// warnings, the short summary and the totals kept whole.</summary>
    public static string? Pytest(string output)
    {
        var lines = Text.Lines(output);
        var hasBanner = lines.Exists(l => Banner(l) is not null);
        var hasTotals = lines.Exists(l => IsTotals(l) || (Banner(l) is { } name && IsTotals(name)));
        if (!hasTotals || !(hasBanner || lines.Exists(IsProgress)))
        {
            return null;
        }

        // A `FAILED id` line already names every failing test; the dots line would repeat it.
        var named = lines.Exists(l =>
            l.StartsWith("FAILED ", StringComparison.Ordinal)
            || l.StartsWith("ERROR ", StringComparison.Ordinal)
        );
        var kept = false;
        var result = new List<string>();
        foreach (var line in lines)
        {
            if (Banner(line) is { } name)
            {
                kept = !name.Contains("test session starts", StringComparison.Ordinal);
                // The `FAILURES` / `short test summary info` frames only label what follows;
                // the totals frame is the one banner worth its tokens.
                if (IsTotals(name))
                {
                    result.Add(line);
                }

                continue;
            }

            if (IsTotals(line) || kept)
            {
                result.Add(line);
            }
            else if (IsProgress(line))
            {
                if (!named && line.AsSpan().IndexOfAny('F', 'E') >= 0)
                {
                    result.Add(line);
                }
            }
            else if (Array.Exists(Problems, w => line.Contains(w, StringComparison.Ordinal)))
            {
                result.Add(line);
            }
        }

        return result.Count == 0 ? null : string.Join('\n', result);
    }

    /// <summary><c>python &lt;script&gt;</c> / <c>python -m unittest</c>: a unittest report is
    /// shortened, a traceback has its repeated frames folded; null when neither applies.</summary>
    public static string? Script(string output)
    {
        if (Unittest(output) is { } got)
        {
            return CollapseFrames(got);
        }

        var folded = CollapseFrames(output);
        return !string.Equals(folded, output, StringComparison.Ordinal) ? folded : null;
    }

    // `unittest`: the dots line goes when it shows no failure; everything else stays.
    internal static string? Unittest(string output)
    {
        if (!Ran().IsMatch(output))
        {
            return null;
        }

        return string.Join(
            '\n',
            Text.Lines(output).Where(l => l.Length == 0 || !l.All(c => c is '.' or 's' or 'x'))
        );
    }

    // Identical consecutive traceback frames (a `File …` line and its code line) folded. Text
    // outside tracebacks is untouched.
    internal static string CollapseFrames(string output)
    {
        var lines = Text.Lines(output);
        var result = new List<string>(lines.Count);
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i++];
            result.Add(line);
            if (!line.StartsWith("Traceback (most recent call last):", StringComparison.Ordinal))
            {
                continue;
            }

            // The frames: indented lines until the exception line.
            var start = i;
            while (i < lines.Count && lines[i].StartsWith(' '))
            {
                i++;
            }

            result.AddRange(Fold(lines.GetRange(start, i - start)));
        }

        var text = string.Join('\n', result);
        return output.EndsWith('\n') ? text + "\n" : text;
    }

    // Frames are `  File …` plus the lines below it up to the next `  File`.
    private static List<string> Fold(List<string> frames)
    {
        var groups = new List<List<string>>();
        foreach (var l in frames)
        {
            if (l.StartsWith("  File ", StringComparison.Ordinal) || groups.Count == 0)
            {
                groups.Add([l]);
            }
            else
            {
                groups[^1].Add(l);
            }
        }

        var result = new List<string>();
        var k = 0;
        while (k < groups.Count)
        {
            var run = 1;
            while (
                k + run < groups.Count
                && groups[k + run].SequenceEqual(groups[k], StringComparer.Ordinal)
            )
            {
                run++;
            }

            result.AddRange(groups[k]);
            if (run > 1)
            {
                result.Add($"  [previous frame repeated {run - 1} more times]");
            }

            k += run;
        }

        return result;
    }

    // `=== name ===`, as pytest prints its sections.
    private static string? Banner(string line)
    {
        var t = line.Trim();
        if (
            Text.StripPrefix(t, "==") is not { } rest
            || !rest.EndsWith("==", StringComparison.Ordinal)
        )
        {
            return null;
        }

        return rest[..^2].Trim('=').Trim();
    }

    // `.F.. [ 40%]` / `tests/a.py ..F`: a progress line.
    private static bool IsProgress(string line) => Progress().IsMatch(line.TrimEnd());

    // `3 passed, 1 failed in 0.10s` without the `===` frame (`-q`).
    private static bool IsTotals(string line) => Totals().IsMatch(line.Trim());

    [GeneratedRegex(@"^\S*\s*[.FEsxXpP]+\s*(\[\s*\d+%\])?$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Progress();

    [GeneratedRegex(
        @"^\d+ (passed|failed|error|errors|skipped|xfailed|xpassed|warnings?|deselected)\b.* in [\d.]+s",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Totals();

    [GeneratedRegex(@"^Ran \d+ tests? in [\d.]+s$", RegexOptions.Multiline, 1000)]
    private static partial Regex Ran();
}
