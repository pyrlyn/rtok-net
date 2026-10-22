using System.Globalization;
using System.Text;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The breadth-first walk of <c>impact</c> over the stored <c>scope</c> edges: who references
/// a name, who references them, up to depth 4. A definition is expanded once, so a call cycle
/// ends. An ambiguous name follows only its resolved definition's references, and an import
/// row takes one hop to the importing file's definitions.
/// </summary>
internal static class ImpactWalk
{
    public const int MaxDepth = 4;

    public static List<ImpactRow> Bfs(ISymbols symbols, string root, string name, int depth)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { name };
        var frontier = new List<string> { name };
        var rows = new List<ImpactRow>();
        for (var d = 1; d <= Math.Clamp(depth, 1, MaxDepth) && frontier.Count > 0; d++)
        {
            var next = new List<string>();
            foreach (var from in frontier)
            {
                var ranked = Resolve.Rank(symbols, root, from);
                foreach (var (path, scope, _, _) in symbols.SymbolRefGroups(root, from))
                {
                    if (!ranked.Allows(path))
                    {
                        continue;
                    }

                    if (scope.Length == 0)
                    {
                        rows.Add(new ImpactRow(d, path, ""));
                    }
                    else if (seen.Add(scope))
                    {
                        rows.Add(new ImpactRow(d, path, scope));
                        next.Add(scope);
                    }
                }

                foreach (var (path, def) in symbols.SymbolImportFollow(root, from))
                {
                    if (seen.Add(def))
                    {
                        rows.Add(new ImpactRow(d, path, def));
                        next.Add(def);
                    }
                }
            }

            frontier = next;
        }

        return rows;
    }

    /// <summary>Rows in answer order: depth, path, name.</summary>
    public static List<ImpactRow> Sorted(IEnumerable<ImpactRow> rows) =>
        [
            .. rows.OrderBy(r => r.Depth)
                .ThenBy(r => r.Path, StringComparer.Ordinal)
                .ThenBy(r => r.Scope, StringComparer.Ordinal),
        ];

    /// <summary>One <c>depth  path  name</c> line per row, <c>(file)</c> for a file-level one.
    /// Shared by <c>impact</c> and <c>explore</c>.</summary>
    public static string Lines(IEnumerable<ImpactRow> rows)
    {
        var output = new StringBuilder();
        foreach (var (d, path, scope) in rows)
        {
            output.Append(
                CultureInfo.InvariantCulture,
                $"{d}  {path}  {(scope.Length == 0 ? "(file)" : scope)}\n"
            );
        }

        return output.ToString();
    }
}
