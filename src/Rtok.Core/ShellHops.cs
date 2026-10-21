namespace Rtok.Core;

/// <summary>
/// Leading <c>cd &lt;dir&gt; &amp;&amp;</c> hops of a shell command. One home for the cmd
/// rewrite, the guard's cd-aware keys and the measure stats.
/// </summary>
public static class ShellHops
{
    /// <summary>
    /// Skips one shell word (bare, or with <c>'…'</c> / <c>"…"</c> segments such as
    /// <c>~/'My Documents'</c>) and returns what follows it, left-trimmed. An unterminated
    /// quote yields null so the caller fails open and leaves the command untouched.
    /// </summary>
    public static string? SkipWord(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        char? quote = null;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (quote is { } q)
            {
                if (c == q)
                {
                    quote = null;
                }
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c is ' ' or '\t' or '\n' or '\f' or '\r')
            {
                return s[i..].TrimStart();
            }
        }

        return quote is null ? "" : null;
    }

    /// <summary>
    /// One <c>cd &lt;dir&gt; &amp;&amp;</c> prefix: the target word (quotes kept as typed) and
    /// the remainder after <c>&amp;&amp;</c>. False unless <c>&amp;&amp;</c> follows the target
    /// at top level: a quoted path with <c>&amp;&amp;</c> inside is one word, not a split point.
    /// </summary>
    public static bool StripCdHop(string s, out string target, out string rest)
    {
        ArgumentNullException.ThrowIfNull(s);
        target = "";
        rest = "";
        if (!s.StartsWith("cd ", StringComparison.Ordinal))
        {
            return false;
        }

        var after = s[3..];
        if (SkipWord(after) is not { } tail)
        {
            return false;
        }

        var word = after[..(after.Length - tail.Length)].TrimEnd();
        if (word.Length == 0 || !tail.StartsWith("&&", StringComparison.Ordinal))
        {
            return false;
        }

        target = word;
        rest = tail[2..].TrimStart();
        return true;
    }
}
