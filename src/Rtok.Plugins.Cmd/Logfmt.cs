namespace Rtok.Plugins.Cmd;

/// <summary>
/// Field summary of an omitted run of logfmt lines (rtok T683). When the middle of a log is
/// <c>key=value</c> lines, the marker says what the dropped lines shared and what changed:
/// constant pairs once, then each varying key with its first and last value.
/// </summary>
internal static class Logfmt
{
    // Shorter runs are cheaper to read than to summarise.
    private const int MinRun = 8;

    // Keys listed in one summary; a wider line is not a log worth describing.
    private const int MaxKeys = 12;

    // A varying value longer than this (in bytes) prints as its key alone.
    private const int MaxValue = 24;

    // A line needs this many pairs to count as a logfmt record.
    private const int MinPairs = 3;

    /// <summary><c>fields: svc=api level=info; varying: ts=T1..T2</c> for a run of logfmt
    /// lines, or <see langword="null"/> when the run is short or its lines share no key.</summary>
    public static string? Summary(IReadOnlyList<string> run)
    {
        if (run.Count < MinRun)
        {
            return null;
        }

        if (Columns(run) is not { } cols)
        {
            return null;
        }

        cols = [.. cols.Take(MaxKeys)];
        var constant = cols.Where(c => c.Constant).Select(c => $"{c.Key}={c.First}").ToList();
        var varying = cols.Where(c => !c.Constant)
            .Select(c =>
                string.Equals(c.First, c.Last, StringComparison.Ordinal)
                || Math.Max(Text.Bytes(c.First), Text.Bytes(c.Last)) > MaxValue
                    ? c.Key
                    : $"{c.Key}={c.First}..{c.Last}"
            )
            .ToList();
        var parts = new List<string>();
        if (constant.Count > 0)
        {
            parts.Add("fields: " + string.Join(' ', constant));
        }

        if (varying.Count > 0)
        {
            parts.Add("varying: " + string.Join(' ', varying));
        }

        return string.Join("; ", parts);
    }

    // (key, first, last, constant) for the keys every line of the run has; null when none.
    private static List<(string Key, string First, string Last, bool Constant)>? Columns(
        IReadOnlyList<string> run
    )
    {
        var cols = Fields(run[0])
            .Select(f => (f.Key, First: f.Value, Last: f.Value, Constant: true))
            .ToList();
        foreach (var line in run.Skip(1))
        {
            var fields = Fields(line);
            for (var c = cols.Count - 1; c >= 0; c--)
            {
                var match = fields.FindIndex(f =>
                    string.Equals(f.Key, cols[c].Key, StringComparison.Ordinal)
                );
                if (match < 0)
                {
                    cols.RemoveAt(c);
                    continue;
                }

                var value = fields[match].Value;
                cols[c] = cols[c] with
                {
                    Constant =
                        cols[c].Constant
                        && string.Equals(value, cols[c].First, StringComparison.Ordinal),
                    Last = value,
                };
            }

            if (cols.Count == 0)
            {
                return null;
            }
        }

        return cols;
    }

    /// <summary>
    /// Strips from every record the pairs all records share and returns them as one
    /// <c>[every record: …]</c> header. <paramref name="anchor"/> lines (errors, warnings) stay
    /// verbatim and do not vote. Nothing changes unless at least eight records, three quarters
    /// of the non-anchor lines, share a pair.
    /// </summary>
    public static string? Factor(List<string> lines, Func<string, bool> anchor)
    {
        var records = Enumerable
            .Range(0, lines.Count)
            .Where(i => !anchor(lines[i]) && Pairs(lines[i]).Count >= MinPairs)
            .ToList();
        var plain = lines.Count(l => !anchor(l));
        if (records.Count < MinRun || records.Count * 4 < plain * 3)
        {
            return null;
        }

        var common = Fields(lines[records[0]]);
        foreach (var i in records.Skip(1))
        {
            var fields = Fields(lines[i]);
            common.RemoveAll(c => !fields.Contains(c));
            if (common.Count == 0)
            {
                return null;
            }
        }

        common = [.. common.Take(MaxKeys)];
        foreach (var i in records)
        {
            var line = lines[i];
            var output = line;
            var pairs = Pairs(line);
            for (var p = pairs.Count - 1; p >= 0; p--)
            {
                var pair = pairs[p];
                if (!common.Contains(new(pair.Key, pair.Value)))
                {
                    continue;
                }

                var end = pair.End;
                while (end < output.Length && IsAsciiWhitespace(output[end]))
                {
                    end++;
                }

                output = output.Remove(pair.Start, end - pair.Start);
            }

            output = output.TrimEnd();
            if (output.Length > 0)
            {
                lines[i] = output;
            }
        }

        return "[every record: " + string.Join(' ', common.Select(c => $"{c.Key}={c.Value}")) + "]";
    }

    private static List<KeyValuePair<string, string>> Fields(string line) =>
        [.. Pairs(line).Select(p => new KeyValuePair<string, string>(p.Key, p.Value))];

    // The key=value pairs of a line, values as written (a "…" value keeps its quotes).
    private static List<Pair> Pairs(string line)
    {
        var output = new List<Pair>();
        var i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && IsAsciiWhitespace(line[i]))
            {
                i++;
            }

            var start = i;
            while (i < line.Length && !IsAsciiWhitespace(line[i]) && line[i] != '=')
            {
                i++;
            }

            var key = line[start..i];
            if (i < line.Length && line[i] == '=' && IsKey(key))
            {
                var valueStart = i + 1;
                i = valueStart;
                if (i < line.Length && line[i] == '"')
                {
                    i++;
                    while (i < line.Length && line[i] != '"')
                    {
                        i += line[i] == '\\' ? 2 : 1;
                    }

                    i++;
                }
                else
                {
                    while (i < line.Length && !IsAsciiWhitespace(line[i]))
                    {
                        i++;
                    }
                }

                var end = Math.Min(i, line.Length);
                output.Add(new(key, line[valueStart..end], start, end));
            }
            else
            {
                while (i < line.Length && !IsAsciiWhitespace(line[i]))
                {
                    i++;
                }
            }
        }

        return output;
    }

    private static bool IsKey(string key) =>
        key.Length > 0
        && (char.IsAsciiLetter(key[0]) || key[0] == '_')
        && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-');

    internal static bool IsAsciiWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';

    private readonly record struct Pair(string Key, string Value, int Start, int End);
}
