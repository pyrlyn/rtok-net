namespace Rtok.Plugins.Cmd;

/// <summary>The <c>group = "dir"</c> and <c>group = "diag"</c> folds.</summary>
internal static class Grouping
{
    // Up to this many path lines are small enough to keep verbatim (rtok T685): folding
    // them drops the dir/name strings an agent greps for and saves little.
    private const int FullPathHits = 50;

    /// <summary>Folds path lines per directory: <c>src/ (12 files): a.rs, b.rs, c.rs …</c>.</summary>
    public static List<string> Dir(List<string> lines)
    {
        // Bare names (ls) have no directory to lose, so only dir/name hits count.
        var hits = lines.Count(l => PathLine(l) is { Dir.Length: > 0 });
        if (hits is >= 1 and <= FullPathHits)
        {
            return lines;
        }

        return Fold(
            lines,
            l => PathLine(l) is { } p ? (p.Dir, "", p.Name) : null,
            (dir, _, names, n) =>
            {
                var list = string.Join(", ", names.Take(3));
                if (names.Count > 3)
                {
                    list += " …";
                }

                return ReplaceFirst($"{(dir.Length == 0 ? "." : dir)}/ ({n} files): {list}");
            }
        );
    }

    /// <summary>Folds diagnostics per code: <c>KEY ×N: msg (loc1, loc2, loc3, …)</c>.</summary>
    public static List<string> Diag(List<string> lines) =>
        Fold(
            lines,
            Diagnostics.Parse,
            (key, msg, locs, n) =>
            {
                var at = string.Join(", ", locs.Take(3));
                if (locs.Count > 3)
                {
                    at += ", …";
                }

                return at.Length == 0 ? $"{key} ×{n}: {msg}" : $"{key} ×{n}: {msg} ({at})";
            }
        );

    // `replacen("./ ", ". ", 1)` on a line that does not start with it.
    private static string ReplaceFirst(string s)
    {
        var at = s.IndexOf("./ ", StringComparison.Ordinal);
        return at < 0 ? s : string.Concat(s.AsSpan(0, at), ". ", s.AsSpan(at + 3));
    }

    private static (string Dir, string Name)? PathLine(string line)
    {
        var s = line.Trim();
        if (s.Length == 0 || s.Any(char.IsWhiteSpace) || s.StartsWith('-'))
        {
            return null;
        }

        if (s.StartsWith("./", StringComparison.Ordinal))
        {
            s = s[2..];
        }

        // A drive letter (C:) is one byte before the colon.
        var drive = s.Length > 1 && s[1] == ':' && char.IsAscii(s[0]);
        if (s.Contains(':', StringComparison.Ordinal) && !drive)
        {
            return null;
        }

        s = s.Replace('\\', '/');
        var slash = s.LastIndexOf('/');
        if (slash >= 0)
        {
            var name = s[(slash + 1)..];
            return name is "" or "." or ".." ? null : (s[..slash], name);
        }

        return s.Contains('.', StringComparison.Ordinal) && s is not "." and not ".."
            ? ("", s)
            : null;
    }

    // Lines `parse` recognises gather under their key at the first one's place; the rest
    // stay where they are.
    private static List<string> Fold(
        List<string> lines,
        Func<string, (string Key, string Msg, string Extra)?> parse,
        Func<string, string, List<string>, int, string> format
    )
    {
        var groups = new List<(string Key, string Msg, List<string> Extra, int Count)>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var output = new List<(int Group, string? Line)>();
        foreach (var line in lines)
        {
            if (parse(line) is not var (key, msg, extra))
            {
                output.Add((-1, line));
                continue;
            }

            if (!index.TryGetValue(key, out var i))
            {
                i = groups.Count;
                index[key] = i;
                groups.Add((key, msg, [], 0));
                output.Add((i, null));
            }

            groups[i] = groups[i] with { Count = groups[i].Count + 1 };
            if (extra.Length > 0)
            {
                groups[i].Extra.Add(extra);
            }
        }

        return
        [
            .. output.Select(e =>
                e.Group < 0
                    ? e.Line!
                    : format(
                        groups[e.Group].Key,
                        groups[e.Group].Msg,
                        groups[e.Group].Extra,
                        groups[e.Group].Count
                    )
            ),
        ];
    }
}
