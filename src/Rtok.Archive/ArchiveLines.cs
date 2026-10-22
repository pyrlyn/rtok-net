using System.Text.RegularExpressions;
using Rtok.Plugin.Sdk;

namespace Rtok.Archive;

/// <summary>The line views of an archived body: <c>--lines</c>, <c>--grep</c>,
/// <c>--context</c> and the <c>[expand] max_lines</c> cap.</summary>
public static class ArchiveLines
{
    /// <summary>The lines of <paramref name="text"/>: split at <c>\n</c>, a <c>\r</c> before
    /// it dropped, no empty last line for a trailing newline.</summary>
    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string> lines = [];
        var at = 0;
        while (at < text.Length)
        {
            var nl = text.IndexOf('\n', at);
            if (nl < 0)
            {
                lines.Add(text[at..]);
                break;
            }
            lines.Add(text[at..(nl > at && text[nl - 1] == '\r' ? nl - 1 : nl)]);
            at = nl + 1;
        }
        return lines;
    }

    /// <summary>
    /// <paramref name="lines"/> cut to <paramref name="range"/>, then, with
    /// <paramref name="grep"/>, only the matching ones as <c>N:line</c>, numbered by their
    /// place in the whole body so a following <c>--lines</c> can pull their surroundings.
    /// <paramref name="grep"/> is a regex; one that does not compile is matched literally.
    /// <paramref name="context"/> &gt; 0 widens each hit by that many lines on both sides,
    /// windows that overlap or touch merged and blocks separated by <c>--</c>; without
    /// <paramref name="grep"/> it is ignored.
    /// </summary>
    public static IReadOnlyList<string> Filter(
        IReadOnlyList<string> lines,
        LineRange? range,
        string? grep,
        int context
    )
    {
        ArgumentNullException.ThrowIfNull(lines);
        var first = range is { } r ? (int)(r.Start - 1) : 0;
        var last = range is { } s ? (int)s.End : lines.Count;
        if (grep is null)
        {
            return [.. lines.Take(last).Skip(first)];
        }
        var pattern = Pattern(grep);
        List<string> rendered = [];
        if (context <= 0)
        {
            for (var i = first; i < last; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    rendered.Add($"{i + 1}:{lines[i]}");
                }
            }
            return rendered;
        }
        List<(int Lo, int Hi)> windows = [];
        for (var i = first; i < last; i++)
        {
            if (!pattern.IsMatch(lines[i]))
            {
                continue;
            }
            var lo = (int)Math.Max(first, (long)i - context);
            var hi = (int)Math.Min(last - 1, (long)i + context);
            if (windows.Count > 0 && lo <= windows[^1].Hi + 1)
            {
                windows[^1] = (windows[^1].Lo, Math.Max(windows[^1].Hi, hi));
            }
            else
            {
                windows.Add((lo, hi));
            }
        }
        foreach (var (lo, hi) in windows)
        {
            if (rendered.Count > 0)
            {
                rendered.Add("--");
            }
            for (var i = lo; i <= hi; i++)
            {
                rendered.Add($"{i + 1}:{lines[i]}");
            }
        }
        return rendered;
    }

    /// <summary><see cref="Filter"/> joined with <c>\n</c>, at most
    /// <paramref name="maxLines"/> of them (0 = all) and then a trailer that names how many
    /// were left out and the short id that brings them back.</summary>
    public static string Render(
        IReadOnlyList<string> lines,
        string id,
        LineRange? range,
        string? grep,
        int context,
        uint maxLines
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        var kept = Filter(lines, range, grep, context);
        var omitted = maxLines > 0 && kept.Count > maxLines ? kept.Count - (int)maxLines : 0;
        var rendered = string.Join('\n', kept.Take(kept.Count - omitted));
        if (omitted > 0)
        {
            var sep = rendered.Length > 0 ? "\n" : "";
            rendered += $"{sep}… {omitted} lines omitted (expand {ArchiveIds.Abbreviate(id)})";
        }
        return rendered;
    }

    private static Regex Pattern(string grep)
    {
        // Non-backtracking: an agent's pattern runs in linear time over a body of any size.
        const RegexOptions options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        try
        {
            return new Regex(grep, options);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return new Regex(Regex.Escape(grep), options);
        }
    }
}
