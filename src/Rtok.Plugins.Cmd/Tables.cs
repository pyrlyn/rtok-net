namespace Rtok.Plugins.Cmd;

/// <summary>
/// Column tables (<c>docker ps</c>, <c>kubectl get</c>, <c>ps aux</c>, <c>df</c>, <c>sqlite3</c>,
/// <c>wc</c>): one compact row per object. Padding and wide columns are alignment noise; the
/// objects are what a head/tail cut of the rule would drop. Each returns null for a body that is
/// not its table, so the rule applies.
/// </summary>
internal static class Tables
{
    // Past the default rule's line cap, its head/tail cut is smaller than one row per object.
    private const int MaxRows = 40;

    private static readonly string[] KubectlKeep = ["NAME", "READY", "STATUS", "IP"];

    /// <summary><c>docker ps</c> and <c>podman ps</c>: bind-all, the container-side port, the
    /// registry host and duration units go; name, status, host port and image tag stay.</summary>
    public static string? DockerPs(string output)
    {
        var lines = Text.Lines(output).FindAll(l => l.Length > 0);
        if (lines.Count == 0)
        {
            return null;
        }

        var u = Text.AsciiLower(lines[0]);
        if (
            !u.Contains("container id", StringComparison.Ordinal)
            && !(
                u.Contains("image", StringComparison.Ordinal)
                && u.Contains("names", StringComparison.Ordinal)
            )
        )
        {
            return null;
        }

        var rows = lines.Skip(1).Select(CompactDockerRow).Where(r => r.Length > 0).ToList();
        return rows.Count == 0 ? null : string.Join('\n', rows);
    }

    /// <summary><c>2 hours</c> → <c>2h</c>. Only a number followed by a unit shortens: docker's
    /// prose forms (<c>About an hour</c>, <c>Less than a second</c>) stay whole words.</summary>
    public static string ShortenAgo(string s)
    {
        static char? Unit(string w) =>
            w switch
            {
                "day" or "days" => 'd',
                "hour" or "hours" => 'h',
                "minute" or "minutes" => 'm',
                "second" or "seconds" => 's',
                "week" or "weeks" => 'w',
                _ => null,
            };

        var words = s.Split(' ');
        var result = new List<string>(words.Length);
        var i = 0;
        while (i < words.Length)
        {
            var w = words[i];
            var number = w.Length > 0 && w.All(char.IsAsciiDigit);
            if (number && i + 1 < words.Length && Unit(words[i + 1]) is { } u)
            {
                result.Add($"{w}{u}");
                i += 2;
            }
            else
            {
                result.Add(w);
                i++;
            }
        }

        return string.Join(' ', result);
    }

    /// <summary><c>kubectl get</c>: NAME, READY, STATUS and IP per object; AGE, NODE and
    /// RESTARTS go. A cell is the text under its header, since a cell can hold spaces.</summary>
    public static string? KubectlGet(string output)
    {
        var lines = Text.Lines(output).FindAll(l => l.Length > 0);
        if (lines.Count == 0)
        {
            return null;
        }

        var header = lines[0];
        var cols = Text.Words(header);
        if (cols is not ["NAME", ..] || !Array.Exists(cols, c => c is "STATUS" or "READY" or "AGE"))
        {
            return null;
        }

        var idx = Enumerable
            .Range(0, cols.Length)
            .Where(i => KubectlKeep.Contains(cols[i]))
            .ToList();
        var starts = Enumerable
            .Range(0, header.Length)
            .Where(i => header[i] != ' ' && (i == 0 || header[i - 1] == ' '))
            .ToList();
        string? Cell(string line, int col)
        {
            if (col >= starts.Count || starts[col] > line.Length)
            {
                return null;
            }

            var from = starts[col];
            var to = col + 1 < starts.Count ? Math.Min(starts[col + 1], line.Length) : line.Length;
            var text = line[from..to].Trim();
            return text.Length == 0 ? null : text;
        }

        var result = new List<string> { string.Join(' ', idx.Select(i => cols[i])) };
        foreach (var line in lines.Skip(1))
        {
            var fields = Text.Words(line);
            if (fields.Length < 2)
            {
                continue;
            }

            // A row that does not line up with the header (proportional output, a cell wider
            // than its column) falls back to token order.
            var aligned =
                starts.Count == cols.Length
                && starts.Skip(1).All(at => at <= line.Length && line[..at].EndsWith(' '));
            var picked = aligned
                ? idx.Select(i => Cell(line, i)).OfType<string>().ToList()
                : idx.Where(i => i < fields.Length).Select(i => fields[i]).ToList();
            if (picked.Count > 0)
            {
                result.Add(string.Join(' ', picked));
            }
        }

        return result.Count < 2 ? null : string.Join('\n', result);
    }

    /// <summary><c>ps aux</c>: PID plus the command basename (and its last arg when that is a
    /// distinct worker id) per process.</summary>
    public static string? PsAux(string output)
    {
        var lines = Text.Lines(output).FindAll(l => l.Length > 0);
        if (lines.Count == 0)
        {
            return null;
        }

        var u = Text.AsciiLower(lines[0]);
        if (
            !u.Contains("pid", StringComparison.Ordinal)
            || !(
                u.Contains("cmd", StringComparison.Ordinal)
                || u.Contains("command", StringComparison.Ordinal)
            )
        )
        {
            return null;
        }

        // The PID column is not always first: BSD `ps aux` prints `USER PID …`.
        var pidAt = Math.Max(
            Array.FindIndex(
                Text.Words(lines[0]),
                h => h.Equals("PID", StringComparison.OrdinalIgnoreCase)
            ),
            0
        );
        var rows = new List<string>();
        foreach (var line in lines.Skip(1))
        {
            var fields = Text.Words(line);
            // One bad line (a repeated header, a wrapped row) skips itself.
            if (pidAt >= fields.Length || !fields[pidAt].All(char.IsAsciiDigit))
            {
                continue;
            }

            var pid = fields[pidAt];
            var cmd =
                Array.Find(
                    fields,
                    t => t.StartsWith('/') || t.StartsWith("./", StringComparison.Ordinal)
                ) ?? fields[^1];
            var bin = cmd[(cmd.LastIndexOf('/') + 1)..];
            var tail = fields[^1];
            rows.Add(
                !string.Equals(tail, bin, StringComparison.Ordinal)
                && !string.Equals(tail, pid, StringComparison.Ordinal)
                    ? $"{pid} {bin} {tail}"
                    : $"{pid} {bin}"
            );
        }

        return rows.Count == 0 ? null : string.Join('\n', rows);
    }

    /// <summary><c>df</c>: one row per mount; inode columns are noise beside used/size.</summary>
    public static string? Df(string output)
    {
        var lines = Text.Lines(output).FindAll(l => !string.IsNullOrWhiteSpace(l));
        if (lines.Count == 0)
        {
            return null;
        }

        var u = Text.AsciiLower(lines[0]);
        if (
            !u.Contains("filesystem", StringComparison.Ordinal)
            || !(
                u.Contains("mounted", StringComparison.Ordinal)
                || u.Contains("capacity", StringComparison.Ordinal)
                || u.Contains("use%", StringComparison.Ordinal)
            )
        )
        {
            return null;
        }

        var rows = lines.Skip(1).Select(CompactDfRow).OfType<string>().ToList();
        return rows.Count == 0 || rows.Count > MaxRows ? null : string.Join('\n', rows);
    }

    /// <summary><c>sqlite3</c>: rule lines (<c>----</c>, <c>+---+</c>) are not cells; padding
    /// collapses.</summary>
    public static string? Sqlite3(string output)
    {
        var lines = Text.Lines(output);
        if (!lines.Exists(IsSqliteRule))
        {
            return null;
        }

        var kept = lines
            .Where(l => !IsSqliteRule(l))
            .Select(l => string.Join(' ', Text.Words(l)))
            .Where(l => l.Length > 0)
            .ToList();
        return kept.Count == 0 || kept.Count > MaxRows ? null : string.Join('\n', kept);
    }

    /// <summary><c>wc</c> right-aligns its counts in padded columns; the numbers, names and the
    /// <c>total</c> row stay, only the padding goes (rtok T684).</summary>
    public static string Wc(string output) =>
        string.Join('\n', Text.Lines(output).Select(l => RuleEngine.CollapseColumns(l.Trim())));

    private static string CompactDockerRow(string line)
    {
        var joined = string.Join(' ', Text.Words(line))
            .Replace("0.0.0.0:", "", StringComparison.Ordinal);
        return ShortenAgo(DropRegistryHost(DropArrowPort(joined)));
    }

    // `->80/tcp`: the container side of a port mapping.
    private static string DropArrowPort(string s)
    {
        var result = new System.Text.StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            if (s[i] == '-' && i + 1 < s.Length && s[i + 1] == '>')
            {
                i += 2;
                while (i < s.Length && char.IsAsciiDigit(s[i]))
                {
                    i++;
                }

                if (i < s.Length && s[i] == '/')
                {
                    i++;
                    while (i < s.Length && char.IsAsciiLetter(s[i]))
                    {
                        i++;
                    }
                }

                continue;
            }

            result.Append(s[i++]);
        }

        return result.ToString();
    }

    private static string DropRegistryHost(string s) =>
        string.Join(
            ' ',
            Text.Words(s)
                .Select(tok =>
                    Text.SplitOnce(tok, "/", out var host, out var rest)
                    && host.Contains('.', StringComparison.Ordinal)
                    && !host.Contains(':', StringComparison.Ordinal)
                        ? rest
                        : tok
                )
        );

    private static string? CompactDfRow(string line)
    {
        var t = Text.Words(line);
        var p = Array.FindIndex(t, tok => tok.EndsWith('%'));
        if (p < 3)
        {
            return null;
        }

        var fs = string.Join(' ', t[..(p - 3)]);
        var i = p + 1;
        while (i < t.Length && !t[i].Contains('/', StringComparison.Ordinal) && IsInodeToken(t[i]))
        {
            i++;
        }

        return fs.Length == 0 || i >= t.Length
            ? null
            : $"{fs} {t[p - 2]}/{t[p - 3]} {t[p]} {string.Join(' ', t[i..])}";
    }

    private static bool IsInodeToken(string tok)
    {
        if (tok is "-" || tok.EndsWith('%'))
        {
            return true;
        }

        var core = tok.TrimEnd('k', 'K', 'm', 'M', 'g', 'G', 't', 'T', 'b', 'B', 'i', 'I');
        return core.Length > 0 && core.All(c => char.IsAsciiDigit(c) || c == '.');
    }

    private static bool IsSqliteRule(string line)
    {
        var t = line.Trim();
        return Text.Bytes(t) >= 3
            && t.All(c => c is '-' or '=' or ' ' or '+' or '|')
            && t.AsSpan().IndexOfAny('-', '=') >= 0;
    }
}
