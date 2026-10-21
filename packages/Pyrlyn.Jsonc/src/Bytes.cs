namespace Pyrlyn.Jsonc;

/// <summary>Scans over whitespace, comments and lines. Called only between tokens, never inside a string.</summary>
internal static class Bytes
{
    /// <summary>The first offset at or after <paramref name="i"/> that is not whitespace or a comment.</summary>
    public static int Skip(ReadOnlySpan<byte> s, int i)
    {
        while (i < s.Length)
        {
            if (IsSpace(s[i]))
            {
                i++;
            }
            else if (Comment(s, i) is var end and > 0)
            {
                i = end;
            }
            else
            {
                break;
            }
        }
        return i;
    }

    /// <summary>
    /// The end of the comment starting at <paramref name="i"/>, or 0. A line comment ends before
    /// its <c>\r</c> or <c>\n</c>, as <see cref="System.Text.Json.Utf8JsonReader"/> ends it.
    /// </summary>
    private static int Comment(ReadOnlySpan<byte> s, int i)
    {
        if (i + 1 >= s.Length || s[i] != (byte)'/')
        {
            return 0;
        }
        if (s[i + 1] == (byte)'/')
        {
            var nl = s[i..].IndexOfAny((byte)'\r', (byte)'\n');
            return nl < 0 ? s.Length : i + nl;
        }
        if (s[i + 1] == (byte)'*')
        {
            var close = s[(i + 2)..].IndexOf("*/"u8);
            return close < 0 ? s.Length : i + 2 + close + 2;
        }
        return 0;
    }

    public static bool IsSpace(byte c) => c is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private static bool IsBlank(byte c) => c is (byte)' ' or (byte)'\t';

    /// <summary>The offset where the line holding <paramref name="i"/> starts.</summary>
    public static int LineStart(ReadOnlySpan<byte> s, int i) => s[..i].LastIndexOf((byte)'\n') + 1;

    /// <summary>Whether only spaces and tabs come before <paramref name="i"/> on its line.</summary>
    public static bool StartsLine(ReadOnlySpan<byte> s, int i)
    {
        var start = LineStart(s, i);
        return s[start..i].IndexOfAnyExcept((byte)' ', (byte)'\t') < 0;
    }

    /// <summary>The spaces and tabs that open the line holding <paramref name="i"/>.</summary>
    public static string Indent(ReadOnlySpan<byte> s, int i)
    {
        var start = LineStart(s, i);
        var rest = s[start..];
        var length = rest.IndexOfAnyExcept((byte)' ', (byte)'\t');
        return System.Text.Encoding.ASCII.GetString(rest[..(length < 0 ? rest.Length : length)]);
    }

    /// <summary>
    /// The offset of the line break that ends the line after <paramref name="i"/> (the <c>\r</c> of a
    /// <c>\r\n</c>), or the end of the text, when only blanks lie between; with
    /// <paramref name="comment"/>, one comment that ends on that line may lie there too. -1 otherwise.
    /// </summary>
    public static int EndOfLine(ReadOnlySpan<byte> s, int i, bool comment)
    {
        while (i < s.Length && IsBlank(s[i]))
        {
            i++;
        }
        if (comment && Comment(s, i) is var end and > 0 && s[i..end].IndexOf((byte)'\n') < 0)
        {
            i = end;
            while (i < s.Length && IsBlank(s[i]))
            {
                i++;
            }
        }
        if (i == s.Length)
        {
            return i;
        }
        if (s[i] == (byte)'\n')
        {
            return i;
        }
        return s[i] == (byte)'\r' && i + 1 < s.Length && s[i + 1] == (byte)'\n' ? i : -1;
    }

    /// <summary>The length of the line break at <paramref name="i"/>: 2 for <c>\r\n</c>, 1 for <c>\n</c>, 0 at the end.</summary>
    public static int BreakLength(ReadOnlySpan<byte> s, int i) =>
        i >= s.Length ? 0
        : s[i] == (byte)'\r' ? 2
        : 1;

    /// <summary>The offset after the spaces and tabs before <paramref name="i"/>, scanning back.</summary>
    public static int BlanksBefore(ReadOnlySpan<byte> s, int i)
    {
        while (i > 0 && IsBlank(s[i - 1]))
        {
            i--;
        }
        return i;
    }

    /// <summary>The offset after the spaces and tabs at <paramref name="i"/>.</summary>
    public static int BlanksAfter(ReadOnlySpan<byte> s, int i)
    {
        while (i < s.Length && IsBlank(s[i]))
        {
            i++;
        }
        return i;
    }
}
