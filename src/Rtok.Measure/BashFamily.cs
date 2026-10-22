namespace Rtok.Measure;

/// <summary>
/// The family a <c>Bash</c> command buckets under: its program's stem, after leading
/// <c>cd &lt;dir&gt; &amp;&amp;</c> and <c>NAME=value</c> prefixes are peeled. A malformed quote fails
/// open: nothing more is peeled.
/// </summary>
internal static class BashFamily
{
    /// <summary>
    /// The family of <paramref name="cmd"/>. <paramref name="visible"/> names the program a
    /// wrapper runs (<c>mise exec -- cargo test</c> → <c>cargo</c>); without it argv[0] stays.
    /// </summary>
    public static string Of(string cmd, Func<IReadOnlyList<string>, string?>? visible = null)
    {
        var s = cmd.Trim();
        while ((StripEnv(s) ?? StripCd(s)) is { } rest && !rest.Equals(s, StringComparison.Ordinal))
        {
            s = rest;
        }
        var words = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var first = words.Length > 0 ? words[0] : "other";
        return Stem(words.Length > 0 && visible?.Invoke(words) is { } vis ? vis : first);
    }

    /// <summary>A program's stem: no folder, no case-insensitive <c>.exe</c>.</summary>
    public static string Stem(string program)
    {
        var name = program[(program.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Length > 4
            ? name[..^4]
            : name;
    }

    /// <summary>
    /// What follows one shell word of <paramref name="s"/>, its leading blanks trimmed: quotes and
    /// backslash escapes are part of the word. Null when a quote never closes.
    /// </summary>
    public static string? SkipWord(string s)
    {
        var i = 0;
        char? quote = null;
        for (; i < s.Length; i++)
        {
            var c = s[i];
            if (quote is { } q)
            {
                if (c == '\\' && q == '"' && i + 1 < s.Length)
                {
                    i++;
                }
                else if (c == q)
                {
                    quote = null;
                }
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '\\' && i + 1 < s.Length)
            {
                i++;
            }
            else if (char.IsWhiteSpace(c))
            {
                break;
            }
        }
        return quote is null ? s[i..].TrimStart() : null;
    }

    private static string? StripEnv(string s)
    {
        var t = s.TrimStart();
        var end = 0;
        while (end < t.Length && (char.IsAsciiLetterOrDigit(t[end]) || t[end] == '_'))
        {
            end++;
        }
        return end > 0 && end < t.Length && t[end] == '=' ? SkipWord(t[(end + 1)..]) : null;
    }

    private static string? StripCd(string s)
    {
        var t = s.TrimStart();
        if (
            !t.StartsWith("cd", StringComparison.Ordinal)
            || t.Length < 3
            || t[2] is not (' ' or '\t')
        )
        {
            return null;
        }
        var after = t[2..].TrimStart();
        var rest = after.StartsWith("&&", StringComparison.Ordinal) ? after : SkipWord(after);
        return rest is not null && rest.StartsWith("&&", StringComparison.Ordinal)
            ? rest[2..].TrimStart()
            : null;
    }
}
