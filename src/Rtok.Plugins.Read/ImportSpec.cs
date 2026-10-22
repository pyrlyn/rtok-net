namespace Rtok.Plugins.Read;

// An import capture as one row per named item: (full specifier, last segment). A brace group is
// one node, so `use a::{b, c::d as e}` becomes `a::b` and `a::c::d`.
internal static class ImportSpec
{
    private static readonly char[] Quotes = ['"', '\'', '`'];
    private static readonly char[] Separators = [':', '.', '/', '\\'];
    private static readonly string[] ModuleSuffixes =
    [
        ".dart",
        ".tsx",
        ".ts",
        ".mjs",
        ".cjs",
        ".jsx",
        ".js",
    ];

    public static List<(string Path, string Name)> Expand(string raw)
    {
        var t = raw.Trim().Trim(Quotes).Trim().TrimEnd(';').Trim();
        var rows = new List<(string, string)>();
        Clause("", t, rows);
        return rows;
    }

    /// <summary>A specifier with a separator is a full path, not a bare last segment.</summary>
    public static bool IsFull(string importPath) => importPath.AsSpan().IndexOfAny(Separators) >= 0;

    public static string LastSegment(string raw)
    {
        var t = raw.Trim().Trim(Quotes);
        foreach (var suffix in ModuleSuffixes)
        {
            if (t.EndsWith(suffix, StringComparison.Ordinal))
            {
                t = t[..^suffix.Length];
                break;
            }
        }

        var end = t.Length;
        while (end > 0)
        {
            var sep = t.AsSpan(0, end).LastIndexOfAny(Separators);
            if (sep < end - 1)
            {
                return t[(sep + 1)..end];
            }

            end = sep;
        }

        return t;
    }

    private static void Clause(string prefix, string clause, List<(string, string)> rows)
    {
        var asAt = clause.IndexOf(" as ", StringComparison.Ordinal);
        clause = TrimEndAll((asAt >= 0 ? clause[..asAt] : clause).Trim(), "::*").Trim();
        var open = clause.IndexOf('{', StringComparison.Ordinal);
        if (open >= 0)
        {
            var close = clause.LastIndexOf('}');
            if (close <= open)
            {
                close = clause.Length;
            }

            var head = TrimEndAll(clause[..open].Trim(), "::").Trim();
            var next =
                prefix.Length == 0 ? head
                : head.Length == 0 ? prefix
                : $"{prefix}::{head}";
            foreach (var item in SplitCommas(clause[(open + 1)..close]))
            {
                Clause(next, item, rows);
            }

            return;
        }

        if (clause is "" or "*" or "self")
        {
            if (prefix.Length > 0)
            {
                One(prefix, rows);
            }

            return;
        }

        One(prefix.Length == 0 ? clause : $"{prefix}::{clause}", rows);
    }

    private static void One(string path, List<(string, string)> rows)
    {
        path = path.Trim();
        if (path.Length > 0)
        {
            rows.Add((path, LastSegment(path)));
        }
    }

    // Commas at brace depth 0, so `a::{b, c}` stays one item inside an outer list.
    private static List<string> SplitCommas(string body)
    {
        var parts = new List<string>();
        var (depth, start) = (0, 0);
        for (var i = 0; i < body.Length; i++)
        {
            switch (body[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(body[start..i]);
                    start = i + 1;
                    break;
            }
        }

        parts.Add(body[start..]);
        return parts;
    }

    private static string TrimEndAll(string s, string suffix)
    {
        while (s.EndsWith(suffix, StringComparison.Ordinal))
        {
            s = s[..^suffix.Length];
        }

        return s;
    }
}
