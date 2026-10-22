using System.Text.RegularExpressions;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// <c>gh</c> output (rtok T676): <c>pr|issue view</c>, <c>pr checks</c>, <c>run view --log</c>, in
/// gh's non-tty (tab separated) shapes, which is what an agent's Bash tool sees. Every function
/// returns null for any other shape or when it would not be shorter, so the <c>gh</c> rule
/// applies as before.
/// </summary>
internal static partial class Gh
{
    // Lines of context kept around an error line inside a failing step.
    private const int Before = 2;
    private const int After = 4;

    private static readonly string[] States = ["pass", "fail", "pending", "skipping", "cancel"];

    /// <summary>The dispatch for <c>gh &lt;area&gt; &lt;verb&gt; …</c>.</summary>
    public static string? Format(IReadOnlyList<string> argv, string output)
    {
        if (argv.Count < 3)
        {
            return null;
        }

        var got = (argv[1], argv[2]) switch
        {
            ("pr" or "issue", "view") => View(output),
            ("pr", "checks") => Checks(output),
            ("run", "view") => RunLog(output),
            _ => null,
        };
        return got is not null && Text.Bytes(got) < Text.Bytes(output) ? got : null;
    }

    // `title:\tX` … `--` body: empty fields go, the body loses its blank-line runs and
    // trailing blanks. Shapes that do not start with a field stand down.
    internal static string? View(string output)
    {
        var lines = Text.Lines(output);
        if (lines.Count == 0 || !Field().IsMatch(lines[0]))
        {
            return null;
        }

        var result = new List<string>();
        var inBody = false;
        var blank = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (inBody)
            {
                if (line.Length == 0)
                {
                    blank = true;
                    continue;
                }

                if (blank && result.Count > 0)
                {
                    result.Add("");
                }

                blank = false;
                result.Add(line);
            }
            else if (line is "--")
            {
                inBody = true;
                result.Add(line);
            }
            else if (
                Field().IsMatch(raw)
                && Text.SplitOnce(raw, "\t", out _, out var value)
                && value.Trim().Length == 0
            )
            {
                // An unset field (`labels:\t`) says nothing.
            }
            else
            {
                result.Add(line);
            }
        }

        return string.Join('\n', result);
    }

    // `name\tstate\telapsed\turl` rows: a count line, then every row that is not
    // `pass`/`skipping` whole (URL included: it is where the failure log lives).
    internal static string? Checks(string output)
    {
        var rows = Text.Lines(output)
            .ConvertAll(l =>
            {
                var cells = l.Split('\t');
                return (State: cells.Length > 1 ? cells[1] : "", Line: l);
            });
        if (rows.Count == 0 || !rows.TrueForAll(r => States.Contains(r.State)))
        {
            return null;
        }

        var parts = States
            .Select(s =>
                (
                    State: s,
                    Count: rows.Count(r => string.Equals(r.State, s, StringComparison.Ordinal))
                )
            )
            .Where(p => p.Count > 0)
            .Select(p => $"{p.Count} {p.State}");
        var result = new List<string> { $"{rows.Count} checks: {string.Join(", ", parts)}" };
        result.AddRange(rows.Where(r => r.State is not ("pass" or "skipping")).Select(r => r.Line));
        return string.Join('\n', result);
    }

    // `job\tstep\t<timestamp> text` log lines grouped by step. A step without a problem line
    // collapses to one count; a step with one keeps those lines plus a little context, each
    // under a `job / step` header, timestamps off.
    internal static string? RunLog(string output)
    {
        if (LogSteps(output) is not { Count: > 0 } steps)
        {
            return null;
        }

        var result = new List<string>();
        var (quiet, quietLines) = (0, 0);
        foreach (var (head, lines) in steps)
        {
            var hits = Enumerable
                .Range(0, lines.Count)
                .Where(i => Problem().IsMatch(lines[i]))
                .ToList();
            if (hits.Count == 0)
            {
                quiet++;
                quietLines += lines.Count;
                continue;
            }

            var keep = new bool[lines.Count];
            foreach (var i in hits)
            {
                for (var k = Math.Max(i - Before, 0); k < Math.Min(i + After + 1, lines.Count); k++)
                {
                    keep[k] = true;
                }
            }

            result.Add($"## {head}");
            var gap = false;
            for (var i = 0; i < lines.Count; i++)
            {
                if (!keep[i])
                {
                    gap = true;
                    continue;
                }

                if (gap)
                {
                    result.Add("  …");
                    gap = false;
                }

                result.Add(lines[i]);
            }
        }

        if (quiet > 0)
        {
            result.Add($"{quiet} steps without errors ({quietLines} lines)");
        }

        return string.Join('\n', result);
    }

    // The log's lines grouped by `job / step`, timestamps off; null when a line is not
    // tab-separated into three columns.
    private static List<(string Head, List<string> Lines)>? LogSteps(string output)
    {
        var steps = new List<(string Head, List<string> Lines)>();
        foreach (var line in Text.Lines(output))
        {
            var cols = line.Split('\t', 3);
            if (cols.Length < 3)
            {
                return null;
            }

            var head = $"{cols[0]} / {cols[1]}";
            var text = Timestamp().Replace(cols[2], "", 1);
            if (steps.Count > 0 && string.Equals(steps[^1].Head, head, StringComparison.Ordinal))
            {
                steps[^1].Lines.Add(text);
            }
            else
            {
                steps.Add((head, [text]));
            }
        }

        return steps;
    }

    [GeneratedRegex(@"^[a-z][a-z-]*:\t", RegexOptions.None, 1000)]
    private static partial Regex Field();

    [GeneratedRegex(@"^\d{4}-\d\d-\d\dT[\d:.]+Z ?", RegexOptions.None, 1000)]
    private static partial Regex Timestamp();

    [GeneratedRegex(
        @"##\[(error|warning)\]|\berror\b|\bfailed\b|\bfailure\b|panicked|\bFAIL\b|warning:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Problem();
}
