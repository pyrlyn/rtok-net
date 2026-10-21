using System.Text;
using System.Text.Json;

namespace Rtok.Plugins.Cmd;

/// <summary>The pure line filter for <c>rtok run</c> output (rtok T3.2). No I/O.</summary>
internal static class RuleEngine
{
    private static readonly string[] BuiltinKeep =
    [
        "error",
        "warning",
        "panic",
        "fail",
        "traceback",
    ];

    private static readonly string[] WarnLevels =
    [
        "level=warn",
        "lvl=warn",
        "[warn]",
        "\"level\":\"warn\"",
    ];

    /// <summary>
    /// Applies <paramref name="rule"/> to <paramref name="output"/>. A failed run
    /// (<paramref name="exit"/> ≠ 0) keeps its last <see cref="CmdSettings.FailTailLines"/> lines,
    /// untouched. <paramref name="archiveId"/> names the archived original in omitted markers.
    /// </summary>
    public static string Apply(
        CmdSettings settings,
        string output,
        int exit,
        Rule rule,
        string archiveId
    ) =>
        ApplyParsed(
            settings,
            output,
            exit == 0 ? JsonView.Parse(output) : null,
            exit,
            rule,
            archiveId
        );

    /// <summary><see cref="Apply"/> for a caller that already parsed the body with
    /// <see cref="JsonView.Parse"/>, so a large JSON body is not parsed twice.</summary>
    public static string ApplyParsed(
        CmdSettings settings,
        string output,
        JsonElement? json,
        int exit,
        Rule rule,
        string archiveId
    )
    {
        if (exit != 0)
        {
            var all = Text.Lines(output);
            return string.Join('\n', all.Skip(Math.Max(all.Count - settings.FailTailLines, 0)));
        }

        // The JSON rewrite replaces every line of the payload, so drop/keep/dedupe could
        // not change it.
        var lines = json is { } v
            ? Text.Lines(
                JsonView.Top(v, (int)Math.Max(rule.JsonItems, 1), (int)Math.Max(rule.JsonString, 1))
            )
            : Filter(output, rule);
        lines = rule.Group switch
        {
            Group.Dir => Grouping.Dir(lines),
            Group.Diag => Grouping.Diag(lines),
            _ => lines,
        };
        var max = (int)Math.Max(rule.MaxLines, 1);
        return lines.Count <= max ? string.Join('\n', lines) : Cut(lines, rule, max, archiveId);
    }

    /// <summary><c>low</c> is the line already ASCII-lowercased.</summary>
    internal static bool IsKeep(string low, Rule rule) =>
        BuiltinKeep.Any(k => low.Contains(k, StringComparison.Ordinal))
        || WarnLevels.Any(k => low.Contains(k, StringComparison.Ordinal))
        || Matches(rule.Keep, low)
        || rule.Group switch
        {
            Group.Dir => low.Contains(" files): ", StringComparison.Ordinal),
            Group.Diag => low.Contains(" ×", StringComparison.Ordinal)
                && low.Contains(": ", StringComparison.Ordinal),
            _ => false,
        };

    /// <summary>
    /// Folds runs of spaces inside a line to one (rtok T65.3); leading indentation is kept
    /// (tracebacks, YAML and diffs are not columnar) and a trailing run folds to nothing.
    /// </summary>
    internal static string CollapseColumns(string line)
    {
        var trimmed = line.TrimStart();
        var output = new StringBuilder(line.Length).Append(
            line.AsSpan(0, line.Length - trimmed.Length)
        );
        var spaces = false;
        foreach (var c in trimmed)
        {
            if (c == ' ')
            {
                spaces = true;
                continue;
            }

            if (spaces)
            {
                output.Append(' ');
                spaces = false;
            }

            output.Append(c);
        }

        return output.ToString();
    }

    // Keep/drop, then the columnar pass (only when no trace block is present: traceback,
    // YAML and diff indentation is not columnar), then dedupe.
    private static List<string> Filter(string output, Rule rule)
    {
        var lines = Text.Lines(output);
        lines.RemoveAll(l =>
        {
            var low = Text.AsciiLower(l);
            return !IsKeep(low, rule) && Matches(rule.Drop, low);
        });
        if (rule.CollapseColumns && !Traces.Blocks(lines).Any(t => t))
        {
            for (var i = 0; i < lines.Count; i++)
            {
                lines[i] = CollapseColumns(lines[i]);
            }
        }

        return rule.Dedupe switch
        {
            Dedupe.Adjacent => Dedupes.Adjacent(lines),
            Dedupe.Normalized => Dedupes.Normalized(lines),
            _ => lines,
        };
    }

    private static bool Matches(IReadOnlyList<string> patterns, string low) =>
        patterns.Any(p =>
            p.Split('|')
                .Any(bit =>
                    bit.Length > 0 && low.Contains(Text.AsciiLower(bit), StringComparison.Ordinal)
                )
        );

    // The head/tail cut. Kept lines and whole trace blocks survive it; each omitted run
    // becomes one marker.
    private static string Cut(List<string> lines, Rule rule, int max, string archiveId)
    {
        // The cut is certain, so state the pairs every record shares once and drop them
        // from the lines that still print (rtok T683); errors and warnings stay verbatim.
        var shared = Logfmt.Factor(lines, l => IsKeep(Text.AsciiLower(l), rule));
        var trace = Traces.Blocks(lines);
        Traces.Cap(trace);
        var take = Picks(lines, rule);
        var cut = new CutState(max, archiveId, shared is not null);
        var total = lines.Count;
        // Suffix count of trace lines still ahead, so the fold into the trailer stays true
        // when trace blocks keep printing past a spent budget.
        var traceAhead = new int[total + 1];
        for (var i = total - 1; i >= 0; i--)
        {
            traceAhead[i] = traceAhead[i + 1] + (trace[i] ? 1 : 0);
        }

        for (var i = 0; i < total; i++)
        {
            if (trace[i])
            {
                cut.Trace(lines[i]);
            }
            else if (take[i] && cut.Picked.Count < max)
            {
                if (cut.Take(lines[i], i, total, traceAhead[i + 1]))
                {
                    break;
                }
            }
            else
            {
                cut.Omit(lines[i]);
            }
        }

        cut.Finish();
        if (shared is not null)
        {
            cut.Picked.Insert(0, shared);
        }

        return string.Join('\n', cut.Picked);
    }

    private static bool[] Picks(List<string> lines, Rule rule)
    {
        var head = (int)Math.Min(rule.Head, rule.MaxLines);
        var tail = (int)Math.Min(rule.Tail, rule.MaxLines - Math.Min(rule.Head, rule.MaxLines));
        var take = new bool[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            take[i] =
                i < head || i >= lines.Count - tail || IsKeep(Text.AsciiLower(lines[i]), rule);
        }

        return take;
    }

    /// <summary>
    /// <c>… N lines omitted (expand id)</c>; with no archive there is nothing to expand, so the
    /// marker names no id. When <paramref name="run"/> is all the omitted lines, a logfmt run
    /// appends what its lines shared and what varied.
    /// </summary>
    internal static string OmittedMarker(int n, string archiveId, List<string> run)
    {
        var marker =
            archiveId.Length == 0
                ? $"… {n} lines omitted"
                : $"… {n} lines omitted (expand {archiveId})";
        return run.Count == n && Logfmt.Summary(run) is { } summary
            ? $"{marker} [{summary}]"
            : marker;
    }

    private sealed class CutState(int max, string archiveId, bool summarise)
    {
        // The lines behind the pending marker, for its logfmt summary; only a logfmt output
        // (one with a shared-pairs header) is summarised.
        private readonly List<string> _run = [];
        private int _omitted;
        private bool _lastTrace;
        private int _markerAt = -1;
        private int _markerCount;

        public List<string> Picked { get; } = [];

        // A trace block's frames are never omitted, even past the budget or behind a trailer.
        public void Trace(string line)
        {
            FlushMarker(_omitted);
            Picked.Add(line);
            _lastTrace = true;
        }

        // A picked line within budget. True when everything left folds into the marker.
        public bool Take(string line, int i, int total, int traceAhead)
        {
            if (_omitted > 0)
            {
                // Past a spent budget only trace blocks print; with none ahead, fold the
                // rest (this line included) and stop. With a trace still ahead the later
                // lines are counted as they come, so folding them here would count twice.
                var fold = Picked.Count + 1 >= max && traceAhead == 0;
                if (fold)
                {
                    // A fold also counts lines never collected, so no summary then.
                    _run.Clear();
                    FlushMarker(_omitted + total - i);
                    return true;
                }

                FlushMarker(_omitted);
            }

            Picked.Add(line);
            _lastTrace = false;
            return false;
        }

        public void Omit(string line)
        {
            _omitted++;
            if (summarise)
            {
                _run.Add(line);
            }
        }

        public void Finish()
        {
            if (_omitted == 0)
            {
                return;
            }

            // Fold the trailer into a full budget only when the last line is not a trace
            // frame; a frame is never popped for it.
            if (Picked.Count >= max && !_lastTrace)
            {
                var popped = Picked[^1];
                Picked.RemoveAt(Picked.Count - 1);
                if (summarise)
                {
                    _run.Add(popped);
                }

                _omitted++;
                // The pop can bare the previous marker: one marker, not two in a row
                // (rtok printed both). Its lines are no longer all in the run, so no summary.
                if (_markerAt == Picked.Count - 1)
                {
                    Picked.RemoveAt(_markerAt);
                    _omitted += _markerCount;
                }
            }

            Picked.Add(OmittedMarker(_omitted, archiveId, _run));
        }

        private void FlushMarker(int count)
        {
            if (count > 0)
            {
                Picked.Add(OmittedMarker(count, archiveId, _run));
                _markerAt = Picked.Count - 1;
                _markerCount = count;
            }

            _omitted = 0;
            _run.Clear();
        }
    }
}
