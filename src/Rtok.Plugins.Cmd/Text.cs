using System.Text;

namespace Rtok.Plugins.Cmd;

/// <summary>String helpers with the semantics rtok's filters were written against.</summary>
internal static class Text
{
    /// <summary>Lines as Rust's <c>str::lines</c> splits them: on <c>\n</c>, one trailing
    /// <c>\r</c> dropped from each, no empty last line after a final <c>\n</c>.</summary>
    public static List<string> Lines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }

            var line = text[start..end];
            lines.Add(line.EndsWith('\r') ? line[..^1] : line);
            start = end + 1;
        }

        return lines;
    }

    /// <summary>ASCII-only lowercase, as Rust's <c>to_ascii_lowercase</c>: other characters
    /// stay as they are.</summary>
    public static string AsciiLower(string text)
    {
        var i = 0;
        while (i < text.Length && !char.IsAsciiLetterUpper(text[i]))
        {
            i++;
        }

        if (i == text.Length)
        {
            return text;
        }

        return string.Create(
            text.Length,
            text,
            static (span, source) =>
            {
                for (var j = 0; j < source.Length; j++)
                {
                    var c = source[j];
                    span[j] = char.IsAsciiLetterUpper(c) ? (char)(c | 0x20) : c;
                }
            }
        );
    }

    /// <summary>Words split on any whitespace, empty ones skipped (Rust's
    /// <c>split_whitespace</c>).</summary>
    public static string[] Words(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Unicode scalar count (Rust's <c>chars().count()</c>).</summary>
    public static int Scalars(string text)
    {
        var n = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            n++;
        }

        return n;
    }

    /// <summary>The first <paramref name="count"/> Unicode scalars of <paramref name="text"/>.</summary>
    public static string TakeScalars(string text, int count)
    {
        var length = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (count-- <= 0)
            {
                break;
            }

            length += rune.Utf16SequenceLength;
        }

        return text[..length];
    }

    /// <summary>UTF-8 byte length (Rust's <c>str::len</c>).</summary>
    public static int Bytes(string text) => Encoding.UTF8.GetByteCount(text);

    /// <summary><paramref name="text"/> with the leading <paramref name="prefix"/> removed,
    /// or <see langword="null"/> when it does not start with it.</summary>
    public static string? StripPrefix(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : null;

    /// <summary><paramref name="text"/> with every leading repeat of <paramref name="prefix"/>
    /// removed (Rust's <c>trim_start_matches</c>).</summary>
    public static string TrimStartMatches(string text, string prefix)
    {
        while (prefix.Length > 0 && text.StartsWith(prefix, StringComparison.Ordinal))
        {
            text = text[prefix.Length..];
        }

        return text;
    }

    /// <summary>Split at the first <paramref name="separator"/>.</summary>
    public static bool SplitOnce(string text, string separator, out string left, out string right)
    {
        var at = text.IndexOf(separator, StringComparison.Ordinal);
        if (at < 0)
        {
            left = right = "";
            return false;
        }

        left = text[..at];
        right = text[(at + separator.Length)..];
        return true;
    }

    /// <summary>Split at the last <paramref name="separator"/>.</summary>
    public static bool RSplitOnce(string text, string separator, out string left, out string right)
    {
        var at = text.LastIndexOf(separator, StringComparison.Ordinal);
        if (at < 0)
        {
            left = right = "";
            return false;
        }

        left = text[..at];
        right = text[(at + separator.Length)..];
        return true;
    }
}
