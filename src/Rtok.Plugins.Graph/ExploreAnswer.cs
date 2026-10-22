using System.Globalization;
using System.Text;

namespace Rtok.Plugins.Graph;

/// <summary>
/// <c>explore(query)</c>: one call answers a code question the way <c>symbol</c>,
/// <c>impact</c> and caller walking did in three to five. The query splits into identifier
/// tokens; each resolves exactly, else by prefix. The answer prints every definition once, the
/// call paths between the resolved names and one depth-1 impact count per name.
/// </summary>
internal static class ExploreAnswer
{
    /// <summary>Distinct names in one answer: the paths are a walk per pair.</summary>
    public const int MaxNames = 10;

    private const int MaxTokens = 8;

    /// <summary>Letter, digit and <c>_</c> runs, distinct, the first 8. A single letter is a
    /// real identifier, so only empty runs are dropped.</summary>
    public static List<string> Tokens(string query)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var run = new StringBuilder();
        foreach (var rune in (query + " ").EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune) || rune.Value == '_')
            {
                run.Append(rune.ToString());
                continue;
            }

            if (run.Length > 0 && seen.Add(run.ToString()))
            {
                tokens.Add(run.ToString());
                if (tokens.Count == MaxTokens)
                {
                    break;
                }
            }

            run.Clear();
        }

        return tokens;
    }

    /// <summary>The names the tokens resolve to, each with the token that found it.</summary>
    public static List<(string Name, string Token)> Names(string query, IExploreParts parts)
    {
        var names = new List<(string Name, string Token)>();
        foreach (var token in Tokens(query))
        {
            foreach (var name in parts.Resolve(token))
            {
                if (!names.Exists(n => string.Equals(n.Name, name, StringComparison.Ordinal)))
                {
                    names.Add((name, token));
                }
            }

            if (names.Count >= MaxNames)
            {
                break;
            }
        }

        return names;
    }

    /// <summary>The answer for <paramref name="names"/>, and the bytes the <c>symbol</c> and
    /// <c>impact</c> calls it replaces would have printed: the Measurement's before.</summary>
    public static (string Text, long Before) Assemble(
        string query,
        GraphFilter filter,
        IExploreParts parts,
        IReadOnlyList<(string Name, string Token)> names
    )
    {
        if (names.Count == 0)
        {
            return ($"no symbols resolved for \"{query}\"{filter.ScopeNote()}", 0);
        }

        var counts = names
            .Select(n => n.Name)
            .ToDictionary(n => n, parts.DefCount, StringComparer.Ordinal);
        var output = new StringBuilder();
        var ambiguous = counts.Values.Count(c => c > 1);
        if (ambiguous > 0)
        {
            output.Append(Ambiguity.Banner(ambiguous));
        }

        long before = 0;
        var impact = new List<string>();
        foreach (var (name, _) in names)
        {
            var defs = parts.Defs(name);
            before += Encoding.UTF8.GetByteCount(defs);
            output.Append("= ").Append(name).Append('\n').Append(defs);
            var (text, rows) = parts.Impact1(name);
            before += Encoding.UTF8.GetByteCount(text);
            impact.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} ← {rows}{(counts[name] > 1 ? " ?" : "")}"
                )
            );
        }

        AppendPaths(output, parts, names);

        output.Append("impact:\n");
        foreach (var line in impact)
        {
            output.Append(line).Append('\n');
        }

        return (output.ToString(), before);
    }

    // Every chain between two of the names, or `none`.
    private static void AppendPaths(
        StringBuilder output,
        IExploreParts parts,
        IReadOnlyList<(string Name, string Token)> names
    )
    {
        output.Append("paths:\n");
        var any = false;
        foreach (var (a, _) in names)
        {
            foreach (var (b, _) in names)
            {
                if (string.Equals(a, b, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var chain in parts.Paths(a, b))
                {
                    output.Append(chain).Append('\n');
                    any = true;
                }
            }
        }

        if (!any)
        {
            output.Append("none\n");
        }
    }
}
