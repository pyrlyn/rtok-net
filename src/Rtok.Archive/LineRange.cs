using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Rtok.Archive;

/// <summary>A 1-based inclusive line range: <c>--lines a-b</c> of <c>rtok expand</c> and the
/// MCP <c>expand</c> tool.</summary>
/// <param name="Start">The first line, at least 1.</param>
/// <param name="End">The last line, at least <paramref name="Start"/>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly partial record struct LineRange(long Start, long End)
{
    /// <summary>
    /// Parses <paramref name="spec"/> against a body of <paramref name="lineCount"/> lines.
    /// <c>a-b</c>, <c>a</c> (to the end), <c>-b</c> (from the start) and <c>a-</c>, plus the
    /// spellings agents send (rtok T172, T353, T522): one pair of surrounding quotes or
    /// brackets, a comma, colon or underscore for the dash, spaces around it, two numbers
    /// separated by a space, quotes around the dash and an HTML break. A number followed by
    /// a break and nothing else is that one line. The end is clamped to
    /// <paramref name="lineCount"/>; a start past it fails, so an empty body rejects every
    /// range.
    /// </summary>
    public static bool TryParse(
        string spec,
        long lineCount,
        out LineRange range,
        [NotNullWhen(false)] out string? error
    )
    {
        ArgumentNullException.ThrowIfNull(spec);
        range = default;
        var trimmed = spec.Trim();
        var shown = (
            trimmed.StartsWith('\'') ? StripPair(trimmed, '\'', '\'') : StripPair(trimmed, '"', '"')
        ).Trim();
        if (!TryLines(shown, out var a, out var b))
        {
            error = $"invalid line range `{shown}`: expected a positive line or a-b";
            return false;
        }
        error =
            a == 0 || b == 0 ? $"invalid line range `{shown}`: lines are 1-based"
            : a > b ? $"invalid line range `{shown}`: start exceeds end"
            : a > lineCount ? $"invalid line range `{shown}`: start exceeds line count {lineCount}"
            : null;
        if (error is not null)
        {
            return false;
        }
        range = new(a, Math.Min(b ?? lineCount, lineCount));
        return true;
    }

    /// <summary>The start and the end (<see langword="null"/>: the last line) that
    /// <paramref name="shown"/> names, once its spelling is brought to <c>a-b</c>.</summary>
    private static bool TryLines(string shown, out long a, out long? b)
    {
        a = 1;
        b = null;
        var norm = StripPair(shown, '[', ']').Replace("\"", "", StringComparison.Ordinal);
        var single = TrailingBreak().IsMatch(norm);
        norm = Break().Replace(norm, "").Trim();
        if (norm.Length == 0)
        {
            return false;
        }
        norm = norm.Replace(',', '-').Replace(':', '-').Replace('_', '-');
        norm = norm.Contains('-', StringComparison.Ordinal)
            ? Spaces().Replace(norm, "")
            : SpacedPair().Replace(norm, "${a}-${b}");
        var dash = norm.IndexOf('-', StringComparison.Ordinal);
        var start = dash < 0 ? norm : norm[..dash];
        var end = dash < 0 ? null : norm[(dash + 1)..];
        if (start.Length > 0 && !TryLine(start, out a))
        {
            return false;
        }
        if (end is { Length: > 0 })
        {
            if (!TryLine(end, out var last))
            {
                return false;
            }
            b = last;
        }
        else if (single && end is null)
        {
            b = a;
        }
        return true;
    }

    private static bool TryLine(string text, out long line) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out line);

    private static string StripPair(string text, char open, char close) =>
        text.Length >= 2 && text[0] == open && text[^1] == close ? text[1..^1] : text;

    [GeneratedRegex(
        @"<br\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Break();

    [GeneratedRegex(
        @"^\s*[0-9]+\s*<br\s*/?>\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex TrailingBreak();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Spaces();

    [GeneratedRegex(
        @"^(?<a>[0-9]+)\s+(?<b>[0-9]+)$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex SpacedPair();
}
