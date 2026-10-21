namespace Rtok.Core;

/// <summary>Terminal escapes, shared by the store's sanitizer and the cmd filters.</summary>
public static class Ansi
{
    /// <summary>Skips one ANSI escape at <paramref name="i"/> (CSI <c>ESC [ … final</c>, OSC
    /// <c>ESC ] … BEL|ESC \</c>, else <c>ESC</c> plus one ASCII character) and returns the index
    /// after it.</summary>
    public static int SkipEscape(ReadOnlySpan<char> s, int i)
    {
        var j = i + 1;
        if (j >= s.Length)
        {
            return j;
        }
        switch (s[j])
        {
            case '[':
                j++;
                while (j < s.Length && s[j] is not (>= '\x40' and <= '\x7e'))
                {
                    j++;
                }
                return Math.Min(j + 1, s.Length);
            case ']':
                for (j++; j < s.Length; j++)
                {
                    if (s[j] == '\a')
                    {
                        return j + 1;
                    }
                    if (s[j] == '\x1b' && j + 1 < s.Length && s[j + 1] == '\\')
                    {
                        return j + 2;
                    }
                }
                return j;
            case < '\x80':
                return j + 1;
            default:
                return j;
        }
    }
}
