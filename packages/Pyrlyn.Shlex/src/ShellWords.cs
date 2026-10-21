using System.Text;

namespace Pyrlyn.Shlex;

/// <summary>POSIX shell words: split a command line the way <c>sh</c> does, and quote words back.</summary>
/// <remarks>
/// Splitting follows the POSIX rules without expansion: words break at unquoted blanks and
/// newlines; single quotes keep everything literal; double quotes let a backslash escape only
/// <c>$</c>, <c>`</c>, <c>"</c>, <c>\</c> and a newline; an unquoted backslash escapes the next
/// character; backslash-newline continues the line; <c>#</c> at the start of a word comments out
/// the rest of the line. Operators such as <c>|</c> or <c>;</c> are ordinary characters here.
/// </remarks>
public static class ShellWords
{
    /// <summary>Splits <paramref name="text"/> into words.</summary>
    /// <param name="text">The command line.</param>
    /// <returns>The words.</returns>
    /// <exception cref="FormatException">A quote is not closed, or the text ends in a backslash.</exception>
    public static IReadOnlyList<string> Split(string text) =>
        TrySplit(text, out var words, out var error) ? words : throw new FormatException(error);

    /// <summary>Splits <paramref name="text"/> into words, reporting instead of guessing when it cannot.</summary>
    /// <param name="text">The command line.</param>
    /// <param name="words">The words, or empty on failure.</param>
    /// <returns>False when a quote is not closed or the text ends in a backslash.</returns>
    public static bool TrySplit(string text, out IReadOnlyList<string> words) =>
        TrySplit(text, out words, out _);

    /// <summary>Splits <paramref name="text"/> into words, with the reason when it cannot.</summary>
    /// <param name="text">The command line.</param>
    /// <param name="words">The words, or empty on failure.</param>
    /// <param name="error">Why it failed, with the 0-based offset, or null.</param>
    /// <returns>False when a quote is not closed or the text ends in a backslash.</returns>
    public static bool TrySplit(string text, out IReadOnlyList<string> words, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        words = [];
        error = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is ' ' or '\t' or '\n')
            {
                if (inWord)
                {
                    result.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }
            }
            else if (c == '#' && !inWord)
            {
                var eol = text.IndexOf('\n', i);
                i = (eol < 0 ? text.Length : eol) - 1;
            }
            else if (c == '\\' && ++i == text.Length)
            {
                error = $"trailing backslash at {i - 1}";
                return false;
            }
            else if (c == '\\')
            {
                // Backslash-newline continues the line.
                inWord |= text[i] != '\n';
                word.Append(text[i] == '\n' ? "" : text[i]);
            }
            else if (c == '\'' && !SingleQuoted(text, ref i, word))
            {
                error = $"unterminated single quote at {i}";
                return false;
            }
            else if (c == '"' && !DoubleQuoted(text, ref i, word))
            {
                error = $"unterminated double quote at {i}";
                return false;
            }
            else
            {
                if (c is not '\'' and not '"')
                {
                    word.Append(c);
                }
                inWord = true;
            }
        }
        if (inWord)
        {
            result.Add(word.ToString());
        }
        words = result;
        return true;
    }

    /// <summary>Reads a single-quoted run starting at the opening quote; leaves <paramref name="i"/> on the closing one.</summary>
    private static bool SingleQuoted(string text, ref int i, StringBuilder word)
    {
        var close = text.IndexOf('\'', i + 1);
        if (close < 0)
        {
            return false;
        }
        word.Append(text, i + 1, close - i - 1);
        i = close;
        return true;
    }

    /// <summary>Reads a double-quoted run starting at the opening quote; leaves <paramref name="i"/> on the closing one.</summary>
    private static bool DoubleQuoted(string text, ref int i, StringBuilder word)
    {
        var open = i;
        for (i++; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                return true;
            }
            if (
                c == '\\'
                && i + 1 < text.Length
                && text[i + 1] is '$' or '`' or '"' or '\\' or '\n'
            )
            {
                if (text[++i] != '\n')
                {
                    word.Append(text[i]);
                }
                continue;
            }
            word.Append(c);
        }
        i = open;
        return false;
    }

    /// <summary>
    /// Quotes one word so <see cref="Split"/> (and <c>sh</c>) reads it back unchanged: as is when
    /// it holds only safe characters, else in single quotes.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The quoted word; <c>''</c> for an empty one.</returns>
    public static string Quote(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (word.Length == 0)
        {
            return "''";
        }
        if (word.All(IsSafe))
        {
            return word;
        }
        return "'" + word.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    /// <summary>Quotes each word and joins them with spaces.</summary>
    /// <param name="words">The words.</param>
    /// <returns>A command line that splits back into <paramref name="words"/>.</returns>
    public static string Join(IEnumerable<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return string.Join(' ', words.Select(Quote));
    }

    private static bool IsSafe(char c) =>
        char.IsAsciiLetterOrDigit(c)
        || c is '@' or '%' or '+' or '=' or ':' or ',' or '.' or '/' or '-' or '_';
}
