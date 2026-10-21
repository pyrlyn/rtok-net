namespace Rtok.Plugins.Cmd;

/// <summary>
/// Stack-trace blocks a head/tail cut must never drop (rtok T65.4): the frames under a kept
/// error or panic line are the part the model needs. One detector per language: Python's
/// traceback header plus its indented frames, Rust's <c>panicked at</c> plus the message and
/// <c>stack backtrace:</c> frames, JS's error line plus <c>    at </c> frames, Go's
/// <c>goroutine N [running]:</c> block and Java's <c>Exception in thread</c> plus tab frames.
/// </summary>
internal static class Traces
{
    /// <summary>Total trace lines printed past the budget: a few full backtraces fit, but an
    /// exit-0 body of nothing but stacks must still be cut.</summary>
    public const int MaxLines = 400;

    /// <summary>For each line, whether it belongs to a trace block.</summary>
    public static bool[] Blocks(IReadOnlyList<string> lines)
    {
        var keep = new bool[lines.Count];
        var i = 0;
        while (i < lines.Count)
        {
            var low = Text.AsciiLower(lines[i]);
            if (low.StartsWith("traceback (most recent call last):", StringComparison.Ordinal))
            {
                keep[i] = true;
                i = Indented(lines, keep, i + 1);
                // The unindented `ValueError: …` line under the frames ends the block and
                // says what went wrong; rtok left it to the budget.
                if (i < lines.Count && lines[i].Length > 0)
                {
                    keep[i++] = true;
                }
            }
            else if (low.Contains("panicked at", StringComparison.Ordinal))
            {
                i = Panic(lines, keep, i);
            }
            else if (
                low.StartsWith("goroutine ", StringComparison.Ordinal)
                && low.Contains('[', StringComparison.Ordinal)
            )
            {
                keep[i] = true;
                i = Mark(
                    lines,
                    keep,
                    i + 1,
                    l => IsIndented(l) || l.TrimEnd().EndsWith("()", StringComparison.Ordinal)
                );
            }
            else if (low.Contains("exception in thread", StringComparison.Ordinal))
            {
                keep[i] = true;
                i = Mark(lines, keep, i + 1, l => l.StartsWith('\t'));
            }
            else if (
                low.Contains("error", StringComparison.Ordinal)
                && i + 1 < lines.Count
                && IsJsFrame(lines[i + 1])
            )
            {
                keep[i] = true;
                i = Mark(lines, keep, i + 1, l => IsJsFrame(l) || l.StartsWith('\t'));
            }
            else
            {
                i++;
            }
        }

        return keep;
    }

    /// <summary>Demotes trace lines past <see cref="MaxLines"/> to ordinary lines, so the
    /// head/tail budget and the omitted marker count them.</summary>
    public static void Cap(bool[] kept)
    {
        var n = 0;
        for (var i = 0; i < kept.Length; i++)
        {
            if (kept[i] && ++n > MaxLines)
            {
                kept[i] = false;
            }
        }
    }

    // Marks lines from `start` while `frame` holds; returns the first line past them.
    private static int Mark(
        IReadOnlyList<string> lines,
        bool[] keep,
        int start,
        Func<string, bool> frame
    )
    {
        var j = start;
        while (j < lines.Count && frame(lines[j]))
        {
            keep[j++] = true;
        }

        return j;
    }

    private static bool IsJsFrame(string line) =>
        line.StartsWith("    at ", StringComparison.Ordinal);

    // The panic message sits directly under the header; from `stack backtrace:` on, the
    // indented frames are kept until the first non-indented line.
    private static int Panic(IReadOnlyList<string> lines, bool[] keep, int i)
    {
        keep[i] = true;
        var j = i + 1;
        if (
            j < lines.Count
            && !IsIndented(lines[j])
            && !EndsBlock(Text.AsciiLower(lines[j]), backtrace: true)
        )
        {
            keep[j++] = true;
        }

        while (j < lines.Count)
        {
            var low = Text.AsciiLower(lines[j]);
            if (low.StartsWith("stack backtrace:", StringComparison.Ordinal))
            {
                keep[j] = true;
                j = Indented(lines, keep, j + 1);
                break;
            }

            if (EndsBlock(low, backtrace: false))
            {
                break;
            }

            if (IsIndented(lines[j]) || j == i + 1)
            {
                keep[j++] = true;
                continue;
            }

            break;
        }

        return j;
    }

    private static bool EndsBlock(string low, bool backtrace) =>
        (backtrace && low.StartsWith("stack backtrace:", StringComparison.Ordinal))
        || low.StartsWith("note:", StringComparison.Ordinal)
        || low.StartsWith("error", StringComparison.Ordinal)
        || low.StartsWith("warning", StringComparison.Ordinal);

    private static int Indented(IReadOnlyList<string> lines, bool[] keep, int from)
    {
        var j = from;
        while (j < lines.Count && IsIndented(lines[j]))
        {
            keep[j++] = true;
        }

        return j;
    }

    private static bool IsIndented(string line) => line.StartsWith(' ') || line.StartsWith('\t');
}
