namespace Rtok.Store;

public sealed partial class RtokStore
{
    // A reference to a frontier name: (matched name, path, enclosing scope), the caller edge.
    private const string ImpactRefs =
        "SELECT name, path, scope FROM symbols WHERE root = $root AND name IN {0} AND is_def = 0 AND name != '' AND kind != 'import'";

    // Definitions in files that import a frontier name: (matched name, path, def name).
    private const string ImpactImportFollow = """
        SELECT i.name, d.path, d.name FROM symbols i JOIN symbols d
            ON d.root = i.root AND d.path = i.path AND d.is_def = 1 AND d.name != ''
        WHERE i.root = $root AND i.name IN {0} AND i.kind = 'import' AND i.is_def = 0
        """;

    // The references that extend a chain through a tip: (referenced name, enclosing scope).
    private const string PathNextHops =
        "SELECT name, scope FROM symbols WHERE root = $root AND name IN {0} AND is_def = 0 AND scope != '' AND kind != 'import'";

    /// <summary>
    /// What reaches <paramref name="name"/> within <paramref name="depth"/> hops (1 to 4), each
    /// <c>(path, scope)</c> at its first depth. A level-by-level walk over two edges, a direct
    /// reference and an import-follow hop; each chain keeps its own seen set, so a name already
    /// on that chain is never emitted for it while an unrelated chain may still reach it. An
    /// empty scope (a file-level reference) is recorded but never followed.
    /// </summary>
    public IReadOnlyList<(int Depth, string Path, string Name)> SymbolImpact(
        string root,
        string name,
        int depth
    )
    {
        var maxDepth = Math.Clamp(depth, 1, 4);
        var results = new Dictionary<(string Path, string Name), int>();
        List<Chain> current = [new(name, new HashSet<string>(StringComparer.Ordinal))];
        using var scope = Enter();
        for (var level = 1; current.Count > 0 && level <= maxDepth; level++)
        {
            var tips = current.Select(c => c.Tip).Distinct(StringComparer.Ordinal).ToList();
            var byTip = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
            GroupByTip(
                Chunked(
                    scope,
                    ImpactRefs,
                    root,
                    tips,
                    r => (r.GetString(0), (r.GetString(1), r.GetString(2)))
                ),
                byTip
            );
            GroupByTip(
                Chunked(
                    scope,
                    ImpactImportFollow,
                    root,
                    tips,
                    r => (r.GetString(0), (r.GetString(1), r.GetString(2)))
                ),
                byTip
            );
            var next = ExtendChains(current, byTip, results, level);
            current = next;
        }

        return results
            .Select(kv => (kv.Value, kv.Key.Path, kv.Key.Name))
            .OrderBy(r => r.Value)
            .ThenBy(r => r.Path, StringComparer.Ordinal)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }

    // One impact level: every chain grows by each edge from its tip whose name it has not seen;
    // a (path, name) keeps the first level that reached it.
    private static List<Chain> ExtendChains(
        List<Chain> current,
        Dictionary<string, List<(string, string)>> byTip,
        Dictionary<(string Path, string Name), int> results,
        int level
    )
    {
        var next = new List<Chain>();
        foreach (var chain in current)
        {
            if (!byTip.TryGetValue(chain.Tip, out var edges))
            {
                continue;
            }

            foreach (var (path, outName) in edges)
            {
                if (chain.Seen.Contains(outName))
                {
                    continue;
                }

                results.TryAdd((path, outName), level);
                if (outName.Length > 0)
                {
                    next.Add(new Chain(outName, [.. chain.Seen, outName]));
                }
            }
        }

        return next;
    }

    /// <summary>
    /// Call chains <c>from → … → to</c> walked in the caller direction (each step a definition
    /// that references the previous one) within <paramref name="depth"/> names (1 to 4). Simple
    /// paths only: a name already on the chain is never revisited and a chain stops at
    /// <paramref name="toName"/>, so cycles end and the shortest come first.
    /// </summary>
    public IReadOnlyList<string> SymbolPaths(string root, string fromName, string toName, int depth)
    {
        var maxDepth = Math.Clamp(depth, 1, 4);
        var results = new List<(int Level, string Chain)>();
        if (string.Equals(fromName, toName, StringComparison.Ordinal))
        {
            results.Add((1, fromName));
        }

        List<Partial> current =
        [
            new(fromName, fromName, new HashSet<string>(StringComparer.Ordinal) { fromName }),
        ];
        using var scope = Enter();
        for (var level = 1; level < maxDepth; level++)
        {
            var active = current
                .Where(p => !string.Equals(p.Tip, toName, StringComparison.Ordinal))
                .ToList();
            if (active.Count == 0)
            {
                break;
            }

            var tips = active.Select(p => p.Tip).Distinct(StringComparer.Ordinal).ToList();
            var byTip = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            GroupByTip(
                Chunked(scope, PathNextHops, root, tips, r => (r.GetString(0), r.GetString(1))),
                byTip
            );
            var next = new List<Partial>();
            foreach (var partial in active)
            {
                if (!byTip.TryGetValue(partial.Tip, out var scopes))
                {
                    continue;
                }

                foreach (var hop in scopes.Where(s => !partial.Seen.Contains(s)))
                {
                    var chain = $"{partial.Chain} → {hop}";
                    if (string.Equals(hop, toName, StringComparison.Ordinal))
                    {
                        results.Add((level + 1, chain));
                    }

                    next.Add(new Partial(chain, hop, [.. partial.Seen, hop]));
                }
            }

            current = next;
        }

        return results
            .OrderBy(r => r.Level)
            .ThenBy(r => r.Chain, StringComparer.Ordinal)
            .Select(r => r.Chain)
            .ToList();
    }

    /// <summary><c>(reference file, definition file)</c> when <paramref name="name"/> has two or
    /// more definitions: the definition files each referencing file's import specifiers
    /// name.</summary>
    public IReadOnlyList<(string RefPath, string DefPath)> SymbolImportedDefs(
        string root,
        string name
    )
    {
        var defs = SymbolDefs(root, name).Select(d => d.Path).ToList();
        if (defs.Count < 2)
        {
            return [];
        }

        List<(string Path, string Spec)> imports;
        using (var scope = Enter())
        {
            using var command = scope.Command(
                "SELECT DISTINCT path FROM symbols WHERE root = $root AND name = $name AND is_def = 0 AND kind != 'import'"
            );
            command.Parameters.AddWithValue("$root", root);
            command.Parameters.AddWithValue("$name", name);
            var refFiles = Rows(command, r => r.GetString(0));
            imports = Chunked(
                scope,
                "SELECT path, scope FROM symbols WHERE root = $root AND kind = 'import' AND is_def = 0 AND path IN {0}",
                root,
                refFiles,
                r => (r.GetString(0), r.GetString(1))
            );
        }

        var keys = defs.Select(d => (Def: d, Key: ImportKeys.FileKey(d))).ToList();
        var seen = new HashSet<(string, string)>();
        foreach (var (refFile, spec) in imports)
        {
            if (ImportKeys.ImportPathSegs(spec) is not { } import)
            {
                continue;
            }

            foreach (var (def, key) in keys)
            {
                if (ImportKeys.KeyPrefixesImport(key, import))
                {
                    seen.Add((refFile, def));
                }
            }
        }

        return seen.OrderBy(p => p.Item1, StringComparer.Ordinal)
            .ThenBy(p => p.Item2, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>One level's rows grouped by the tip they matched, a repeated value under the same
    /// tip kept once.</summary>
    private static void GroupByTip<T>(
        List<(string Tip, T Value)> rows,
        Dictionary<string, List<T>> byTip
    )
    {
        foreach (var (tip, value) in rows)
        {
            if (!byTip.TryGetValue(tip, out var values))
            {
                byTip[tip] = values = [];
            }

            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }
    }

    private sealed record Chain(string Tip, HashSet<string> Seen);

    private sealed record Partial(string Chain, string Tip, HashSet<string> Seen);
}
