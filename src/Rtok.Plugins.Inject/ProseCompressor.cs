using System.Text;

namespace Rtok.Plugins.Inject;

/// <summary>
/// A deterministic English fluff stripper (rtok <c>compress_prose</c>): the safety net under
/// the terse mode when a model ignores it. It never touches a fenced code block or an inline
/// backtick span, never drops a negation (<c>not</c>, <c>never</c>, <c>no</c>, <c>only</c>,
/// <c>except</c>), and never drops a word glued to a name or path (<c>foo_a</c>,
/// <c>/tmp/a</c>, <c>item1a</c>). Its output is never longer than its input.
/// </summary>
public static class ProseCompressor
{
    // Order matters: the first phrase that matches is the one stripped.
    private static readonly string[] Pleasantries =
    [
        "I'd be happy to help you with that.",
        "I'd be happy to help you with that",
        "I'd be happy to help!",
        "I'd be happy to help.",
        "I'd be happy to help",
        "I would be happy to help.",
        "I would be happy to help",
        "Happy to help!",
        "Happy to help.",
        "Of course!",
        "Of course.",
        "Certainly!",
        "Certainly.",
        "Sure thing!",
        "Sure thing.",
        "Sure!",
        "Sure.",
    ];

    private static readonly HashSet<string> Filler = new(
        ["just", "really", "basically", "actually", "simply", "literally"],
        StringComparer.Ordinal
    );

    private static readonly HashSet<string> Articles = new(
        ["a", "an", "the"],
        StringComparer.Ordinal
    );

    private static readonly string[] ConjunctionPad = ["and then", "and also", "as well as"];

    private static readonly HashSet<string> Keep = new(
        ["not", "never", "no", "only", "except"],
        StringComparer.Ordinal
    );

    // Private-use, never whitespace: marks a segment edge that must keep its spacing.
    private const char Edge = '';

    /// <summary>
    /// <paramref name="text"/> with fluff stripped at <paramref name="intensity"/> outside code,
    /// and whitespace collapsed within each prose line. Fenced blocks come back byte for byte.
    /// </summary>
    public static string Compress(string text, ProseIntensity intensity)
    {
        ArgumentNullException.ThrowIfNull(text);
        var output = new StringBuilder(text.Length);
        var rest = text;
        var afterFence = false;
        while (FindFence(rest) is (var start, var end))
        {
            output.Append(Outside(rest[..start], intensity, afterFence));
            output.Append(rest, start, end - start);
            rest = rest[end..];
            afterFence = true;
        }

        output.Append(Outside(rest, intensity, afterFence));
        return output.ToString();
    }

    /// <summary>
    /// The first fenced code block in <paramref name="text"/>: from its opening line's start to
    /// the end of its closing run, or to the end of the text when it is never closed.
    /// </summary>
    /// <remarks>
    /// CommonMark: an opener has at most three spaces of indent and a run of three or more
    /// backticks or tildes (a backtick info string holds no backtick, or the run is an inline
    /// span); only a line with the same character, a run at least as long and nothing else
    /// closes it, so a shorter run inside a longer fence stays code.
    /// </remarks>
    internal static (int Start, int End)? FindFence(string text)
    {
        var at = 0;
        (int Start, char Ch, int Len)? open = null;
        while (at < text.Length)
        {
            var newline = text.IndexOf('\n', at);
            var next = newline < 0 ? text.Length : newline + 1;
            var line = text.AsSpan(at, next - at);
            if (FenceRun(line, out var ch, out var len, out var tail))
            {
                if (open is null)
                {
                    if (ch != '`' || !tail.Contains('`'))
                    {
                        open = (at, ch, len);
                    }
                }
                else if (ch == open.Value.Ch && len >= open.Value.Len && tail.Trim().IsEmpty)
                {
                    return (open.Value.Start, next - tail.Length);
                }
            }

            at = next;
        }

        return open is { } o ? (o.Start, text.Length) : null;
    }

    // Fence character, run length and the rest of the line, when the line starts a fence run.
    private static bool FenceRun(
        ReadOnlySpan<char> line,
        out char ch,
        out int run,
        out ReadOnlySpan<char> tail
    )
    {
        var body = line.TrimStart(' ');
        ch = body.IsEmpty ? '\0' : body[0];
        run = 0;
        tail = default;
        if (line.Length - body.Length > 3 || ch is not ('`' or '~'))
        {
            return false;
        }

        run = body.IndexOfAnyExcept(ch);
        if (run < 0)
        {
            run = body.Length;
        }

        tail = body[run..];
        return run >= 3;
    }

    // An unfenced chunk. After a fence it starts with the rest of the closing line; that line
    // break is kept, or the next line would glue onto the closer and unmake it.
    private static string Outside(string prose, ProseIntensity intensity, bool afterFence)
    {
        if (afterFence && prose.IndexOf('\n') is var newline and >= 0)
        {
            return "\n" + Segments(prose[(newline + 1)..], intensity);
        }

        return afterFence ? "" : Segments(prose, intensity);
    }

    private static string Segments(string prose, ProseIntensity intensity)
    {
        var segments = InlineSegments(prose);
        var output = new StringBuilder(prose.Length);
        for (var index = 0; index < segments.Count; index++)
        {
            var (segment, isProse) = segments[index];
            if (!isProse)
            {
                output.Append(segment);
                continue;
            }

            var s = index == 0 ? StripLeadingPleasantries(segment) : segment;
            if (intensity is ProseIntensity.Full or ProseIntensity.Ultra)
            {
                s = StripWords(s, Filler);
                s = StripWords(s, Articles);
                if (intensity == ProseIntensity.Ultra)
                {
                    foreach (var pad in ConjunctionPad)
                    {
                        s = ReplaceStandalone(s, pad, " ");
                    }
                }
            }

            output.Append(CleanupSegment(s, index > 0, index < segments.Count - 1));
        }

        return output.ToString();
    }

    // Prose and matching backtick spans, in order. An unmatched opener protects the rest
    // rather than risk editing code.
    private static List<(string Text, bool IsProse)> InlineSegments(string text)
    {
        var segments = new List<(string, bool)>();
        var proseStart = 0;
        var cursor = 0;
        while (cursor < text.Length)
        {
            if (text[cursor] != '`')
            {
                cursor++;
                continue;
            }

            var opener = cursor;
            cursor = RunEnd(text, cursor);
            var delimiter = cursor - opener;
            int? closingEnd = null;
            while (cursor < text.Length)
            {
                if (text[cursor] != '`')
                {
                    cursor++;
                    continue;
                }

                var closing = cursor;
                cursor = RunEnd(text, cursor);
                if (cursor - closing == delimiter)
                {
                    closingEnd = cursor;
                    break;
                }
            }

            if (proseStart < opener)
            {
                segments.Add((text[proseStart..opener], true));
            }

            var end = closingEnd ?? text.Length;
            segments.Add((text[opener..end], false));
            proseStart = end;
        }

        if (proseStart < text.Length)
        {
            segments.Add((text[proseStart..], true));
        }

        return segments;
    }

    private static int RunEnd(string text, int at)
    {
        while (at < text.Length && text[at] == '`')
        {
            at++;
        }

        return at;
    }

    // Only whole phrases, each followed by the end or a character that cannot continue it.
    private static string StripLeadingPleasantries(string text)
    {
        var rest = text;
        while (true)
        {
            var trimmed = rest.TrimStart();
            var phrase = Pleasantries.FirstOrDefault(p =>
                StartsWithAsciiIgnoreCase(trimmed, p)
                && (
                    trimmed.Length == p.Length
                    || !(char.IsAsciiLetterOrDigit(trimmed[p.Length]) || trimmed[p.Length] == '\'')
                )
            );
            if (phrase is null)
            {
                return rest;
            }

            rest = trimmed[phrase.Length..];
        }
    }

    private static bool StartsWithAsciiIgnoreCase(ReadOnlySpan<char> text, string prefix)
    {
        if (text.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (char.ToLowerInvariant(prefix[i]) != AsciiLower(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static char AsciiLower(char c) => char.IsAsciiLetterUpper(c) ? (char)(c | 0x20) : c;

    // Replaces standalone, case-insensitive matches of an ASCII phrase. A match that runs into
    // a longer word stays: `and then` must not turn `command then` into `comm`.
    private static string ReplaceStandalone(string hay, string needle, string with)
    {
        var output = new StringBuilder(hay.Length);
        var i = 0;
        while (IndexOfAsciiIgnoreCase(hay, needle, i) is var at and >= 0)
        {
            var end = at + needle.Length;
            var joined =
                (at > 0 && IsWordChar(hay[at - 1])) || (end < hay.Length && IsWordChar(hay[end]));
            if (joined)
            {
                output.Append(hay, i, at + 1 - i);
                i = at + 1;
                continue;
            }

            output.Append(hay, i, at - i).Append(with);
            i = end;
        }

        return output.Append(hay, i, hay.Length - i).ToString();
    }

    private static int IndexOfAsciiIgnoreCase(string hay, string needle, int from)
    {
        for (var at = from; at + needle.Length <= hay.Length; at++)
        {
            if (StartsWithAsciiIgnoreCase(hay.AsSpan(at), needle))
            {
                return at;
            }
        }

        return -1;
    }

    // Any non-ASCII character may be part of a letter, so it continues a word.
    private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c >= 0x80;

    // Drops whole ASCII words from `words`, never a negation and never a word glued to a name
    // on either side. The left neighbour is read from the input, never from edited output.
    private static string StripWords(string text, HashSet<string> words)
    {
        var output = new StringBuilder(text.Length);
        Rune? previous = null;
        var at = 0;
        while (at < text.Length)
        {
            Rune.DecodeFromUtf16(text.AsSpan(at), out var rune, out var width);
            if (!(rune.IsAscii && char.IsAsciiLetter((char)rune.Value)))
            {
                output.Append(text, at, width);
                previous = rune;
                at += width;
                continue;
            }

            var start = at;
            while (at < text.Length && (char.IsAsciiLetter(text[at]) || text[at] == '\''))
            {
                at++;
            }

            var word = text[start..at];
            var lower = word.ToLowerInvariant();
            var glued =
                (previous is { } p && IsNameGlue(p))
                || (at < text.Length && IsNameGlue(NextRune(text, at)));
            if (glued || Keep.Contains(lower) || !words.Contains(lower))
            {
                output.Append(word);
            }

            previous = new Rune(word[^1]);
        }

        return output.ToString();
    }

    // A lone surrogate reads as U+FFFD, which glues nothing.
    private static Rune NextRune(string text, int at)
    {
        Rune.DecodeFromUtf16(text.AsSpan(at), out var rune, out _);
        return rune;
    }

    // Joins a word to a neighbouring name or path: `a.txt`, `a-b`, `a/b`, `item1`, `foo_a`,
    // `naïve`. A combining mark counts too: it belongs to the letter before it.
    private static bool IsNameGlue(Rune rune) =>
        rune.Value is '.' or '-' or '_' or '/' or '\\'
        || (rune.IsAscii && char.IsAsciiDigit((char)rune.Value))
        || (
            !rune.IsAscii
            && (
                Rune.IsLetterOrDigit(rune)
                || Rune.IsNumber(rune)
                || Rune.GetUnicodeCategory(rune)
                    is System.Globalization.UnicodeCategory.NonSpacingMark
                        or System.Globalization.UnicodeCategory.SpacingCombiningMark
            )
        );

    // Collapses whitespace like CleanupWhitespace, but keeps the edge next to a backtick span.
    private static string CleanupSegment(string s, bool keepStart, bool keepEnd)
    {
        var padded = (keepStart ? Edge.ToString() : "") + s + (keepEnd ? Edge.ToString() : "");
        var cleaned = CleanupWhitespace(padded);
        if (keepStart)
        {
            cleaned = cleaned[1..];
        }

        if (keepEnd)
        {
            cleaned = cleaned[..^1];
        }

        return cleaned;
    }

    // Per line (split on '\n', one trailing '\r' dropped): whitespace runs become one space,
    // line edges are trimmed; a final line break stays, leading whitespace goes.
    private static string CleanupWhitespace(string s)
    {
        var output = new StringBuilder(s.Length);
        var lines = s.Split('\n');
        var count = s.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        for (var i = 0; i < count; i++)
        {
            var line = lines[i].EndsWith('\r') ? lines[i][..^1] : lines[i];
            if (i > 0)
            {
                output.Append('\n');
            }

            var space = true;
            foreach (var c in line)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!space)
                    {
                        output.Append(' ');
                        space = true;
                    }
                }
                else
                {
                    output.Append(c);
                    space = false;
                }
            }

            while (output.Length > 0 && output[^1] == ' ')
            {
                output.Length--;
            }
        }

        if (s.EndsWith('\n'))
        {
            output.Append('\n');
        }

        return output.ToString().TrimStart();
    }
}
