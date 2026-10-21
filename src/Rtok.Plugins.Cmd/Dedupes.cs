using System.Text;

namespace Rtok.Plugins.Cmd;

/// <summary>Adjacent and normalized folding of repeated lines (rtok T64.2).</summary>
internal static class Dedupes
{
    // Character length of the d:d:d clock form (1:2:3).
    private const int ClockLen = 5;

    /// <summary>A run of equal lines becomes <c>line (×N)</c>.</summary>
    public static List<string> Adjacent(List<string> lines)
    {
        var output = new List<string>();
        string? prev = null;
        var n = 0;
        foreach (var line in lines)
        {
            if (string.Equals(prev, line, StringComparison.Ordinal))
            {
                n++;
                continue;
            }

            Flush(output, prev, n);
            prev = line;
            n = 1;
        }

        Flush(output, prev, n);
        return output;
    }

    /// <summary>
    /// Lines equal once timestamps, pids, ids, long hex and durations are masked fold into the
    /// first one: <c>line (×N, also lines 4, 9)</c>.
    /// </summary>
    public static List<string> Normalized(List<string> lines)
    {
        var keys = lines.Select(NormalizeKey).ToList();
        var first = new Dictionary<string, int>(StringComparer.Ordinal);
        var also = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < keys.Count; i++)
        {
            if (first.TryAdd(keys[i], i))
            {
                also[keys[i]] = [];
            }
            else
            {
                also[keys[i]].Add(i + 1);
            }
        }

        var output = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            var key = keys[i];
            if (first[key] != i)
            {
                continue;
            }

            var more = also[key];
            output.Add(
                more.Count > 0
                    ? $"{lines[i]} (×{more.Count + 1}, also lines {string.Join(", ", more)})"
                    : lines[i]
            );
        }

        return output;
    }

    internal static string NormalizeKey(string line)
    {
        var output = new StringBuilder(line.Length);
        var i = 0;
        while (i < line.Length)
        {
            var rest = line.AsSpan(i);
            if (Placeholder(rest) is var (n, replacement))
            {
                output.Append(replacement);
                i += n;
                continue;
            }

            output.Append(line[i]);
            i++;
        }

        return output.ToString();
    }

    private static void Flush(List<string> output, string? prev, int n)
    {
        if (prev is not null)
        {
            output.Add(n > 1 ? $"{prev} (×{n})" : prev);
        }
    }

    private static (int Length, string Replacement)? Placeholder(ReadOnlySpan<char> rest)
    {
        if (IsIsoTime(rest))
        {
            return (19, "<TS>");
        }

        if (IsClfTime(rest))
        {
            return (20, "<TS>");
        }

        if (rest.StartsWith("pid=", StringComparison.Ordinal))
        {
            var j = 4;
            while (j < rest.Length && char.IsAsciiDigit(rest[j]))
            {
                j++;
            }

            if (j > 4)
            {
                return (j, "pid=<PID>");
            }
        }

        if (UuidAt(rest) is { } uuid)
        {
            return (uuid, "<ID>");
        }

        if (LongHexAt(rest) is { } hex)
        {
            return (hex, "<HEX>");
        }

        if (rest.Length > 0 && char.IsAsciiDigit(rest[0]) && DurationAt(rest) is { } duration)
        {
            return (duration, "<DUR>");
        }

        return null;
    }

    // 2024-01-02T03:04:05 or with a space between date and time.
    private static bool IsIsoTime(ReadOnlySpan<char> b) =>
        b.Length >= 19
        && b[4] == '-'
        && b[7] == '-'
        && b[10] is 'T' or ' '
        && b[13] == ':'
        && b[16] == ':'
        && Digits(b[0..4])
        && Digits(b[5..7])
        && Digits(b[8..10])
        && Digits(b[11..13])
        && Digits(b[14..16])
        && Digits(b[17..19]);

    // 02/Jan/2024:03:04:05, the common log format.
    private static bool IsClfTime(ReadOnlySpan<char> b) =>
        b.Length >= 20
        && b[2] == '/'
        && b[6] == '/'
        && b[11] == ':'
        && b[14] == ':'
        && b[17] == ':'
        && Digits(b[0..2])
        && Digits(b[7..11])
        && Digits(b[12..14])
        && Digits(b[15..17])
        && Digits(b[18..20])
        && char.IsAsciiLetter(b[3])
        && char.IsAsciiLetter(b[4])
        && char.IsAsciiLetter(b[5]);

    private static bool Digits(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static int? UuidAt(ReadOnlySpan<char> rest)
    {
        ReadOnlySpan<int> lengths = [8, 4, 4, 4, 12];
        var pos = 0;
        for (var s = 0; s < lengths.Length; s++)
        {
            var len = lengths[s];
            if (pos + len > rest.Length)
            {
                return null;
            }

            foreach (var c in rest.Slice(pos, len))
            {
                if (!char.IsAsciiHexDigit(c))
                {
                    return null;
                }
            }

            pos += len;
            if (s < lengths.Length - 1)
            {
                if (pos >= rest.Length || rest[pos] != '-')
                {
                    return null;
                }

                pos++;
            }
        }

        return pos;
    }

    private static int? LongHexAt(ReadOnlySpan<char> rest)
    {
        if (
            rest.StartsWith("0x", StringComparison.Ordinal)
            || rest.StartsWith("0X", StringComparison.Ordinal)
        )
        {
            return null;
        }

        var n = 0;
        while (n < rest.Length && char.IsAsciiHexDigit(rest[n]))
        {
            n++;
        }

        return n >= 16 ? n : null;
    }

    // s / ms only at a token edge: 1src is a path, not a one-second duration.
    private static bool DurationEdge(ReadOnlySpan<char> rest, int n) =>
        n >= rest.Length || !char.IsAsciiLetterOrDigit(rest[n]);

    private static int? DurationAt(ReadOnlySpan<char> b)
    {
        if (
            b.Length >= ClockLen
            && char.IsAsciiDigit(b[0])
            && b[1] == ':'
            && char.IsAsciiDigit(b[2])
            && b[3] == ':'
            && char.IsAsciiDigit(b[4])
            && DurationEdge(b, ClockLen)
        )
        {
            return ClockLen;
        }

        var i = 0;
        while (i < b.Length && (char.IsAsciiDigit(b[i]) || b[i] == '.'))
        {
            i++;
        }

        if (i == 0)
        {
            return null;
        }

        if (b[i..].StartsWith("ms", StringComparison.Ordinal) && DurationEdge(b, i + 2))
        {
            return i + 2;
        }

        if (b[i..].StartsWith("s", StringComparison.Ordinal) && DurationEdge(b, i + 1))
        {
            return i + 1;
        }

        return null;
    }
}
