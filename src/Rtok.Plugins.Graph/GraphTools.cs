using System.Globalization;
using System.Text;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The five navigation tools over the tags index of one project root: <c>symbol</c>,
/// <c>callers</c>, <c>impact</c>, <c>outline</c> and <c>explore</c> (rtok
/// <c>graph/mod.rs</c>). Every answer is capped with its full text archived, leads with the
/// pending banner when the index lags, and marks a name with several definitions.
/// </summary>
public static class GraphTools
{
    /// <summary>Definitions of <paramref name="name"/>: id and <c>path:line kind</c>, the
    /// body, the names it calls.</summary>
    public static string Symbol(Ctx cx, string root, string name, GraphFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(cx);
        filter ??= GraphFilter.None;
        var key = Stale.IndexFor(cx, root);
        var rows = cx
            .Host.SymbolDefs(key, name)
            .Where(r => filter.PathOk(r.Path) && filter.KindOk(r.Kind))
            .ToList();
        return rows.Count == 0
            ? Stale.With(cx, key, $"no definition of {name}{filter.ScopeNote()}")
            : Stale.With(cx, key, Cap.Apply(cx, Bodies.Text(cx, key, name, rows)));
    }

    /// <summary><see cref="Symbol"/> of several names, each under <c>= name</c>, capped
    /// together. One name is the plain answer.</summary>
    public static string Symbols(
        Ctx cx,
        string root,
        IReadOnlyList<string> names,
        GraphFilter? filter = null
    )
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count <= 1)
        {
            return Symbol(cx, root, names.Count == 0 ? "" : names[0], filter);
        }

        var output = new StringBuilder();
        foreach (var name in names)
        {
            var body = Symbol(cx, root, name, filter);
            if (output.Length > 0 && output[^1] != '\n')
            {
                output.Append('\n');
            }

            output.Append("= ").Append(name).Append('\n').Append(body);
            if (!body.EndsWith('\n'))
            {
                output.Append('\n');
            }
        }

        return Cap.Apply(cx, output.ToString());
    }

    /// <summary>The one definition <paramref name="id"/> (<c>path::name#kind@line</c>)
    /// names.</summary>
    public static string SymbolById(Ctx cx, string root, string id)
    {
        ArgumentNullException.ThrowIfNull(cx);
        if (Bodies.ParseId(id) is not var (path, name, kind, line))
        {
            return $"no definition of {id}";
        }

        var key = Stale.IndexFor(cx, root);
        var rows = cx
            .Host.SymbolDefs(key, name)
            .Where(r =>
                string.Equals(r.Path, path, StringComparison.Ordinal)
                && string.Equals(r.Kind, kind, StringComparison.Ordinal)
                && r.Line == line
            )
            .ToList();
        return rows.Count == 0
            ? Stale.With(cx, key, $"no definition of {id}")
            : Stale.With(cx, key, Cap.Apply(cx, Bodies.Text(cx, key, name, rows)));
    }

    /// <summary>One <c>path  caller ×N (Lline)</c> line per referencing definition.</summary>
    public static string Callers(Ctx cx, string root, string name, GraphFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(cx);
        filter ??= GraphFilter.None;
        var key = Stale.IndexFor(cx, root);
        var ranked = Resolve.Rank(cx.Host, key, name);
        var defs = cx.Host.SymbolDefs(key, name).Count;
        var rows = cx
            .Host.SymbolRefGroups(key, name)
            .Where(r => filter.PathOk(r.Path) && ranked.Allows(r.Path))
            .ToList();
        if (rows.Count == 0)
        {
            return Stale.With(
                cx,
                key,
                Ambiguity.Flag(defs, $"no references to {name}{filter.ScopeNote()}")
            );
        }

        var output = new StringBuilder();
        foreach (var (path, scope, count, line) in rows)
        {
            output.Append(
                CultureInfo.InvariantCulture,
                $"{path}{(scope.Length == 0 ? "" : "  " + scope)} ×{count} (L{line})\n"
            );
        }

        output.Append(OtherDefs(name, ranked.Others));
        return Stale.With(cx, key, Cap.Apply(cx, Ambiguity.Flag(defs, output.ToString())));
    }

    /// <summary>What breaks if <paramref name="name"/> changes: the definitions that reach it
    /// up to <paramref name="depth"/> (at most 4), within <c>impact_tokens</c>, then the files
    /// that change with its file. With <paramref name="to"/>, the call chains from
    /// <paramref name="name"/> to it instead.</summary>
    public static string Impact(
        Ctx cx,
        string root,
        string name,
        int depth = 2,
        GraphFilter? filter = null,
        string? to = null
    )
    {
        ArgumentNullException.ThrowIfNull(cx);
        filter ??= GraphFilter.None;
        var key = Stale.IndexFor(cx, root);
        var defs = cx.Host.SymbolDefs(key, name);
        if (!string.IsNullOrEmpty(to))
        {
            return Chains(cx, key, name, to, depth, defs.Count);
        }

        var ranked = Resolve.Rank(cx.Host, key, name);
        // One definition is its file; an unresolved name may itself be a path.
        var file = ranked.DefPath ?? (defs.Count == 1 ? defs[0].Path : name);
        var changes = Cochange.ChangesWith(cx, key, file);
        var rows = ImpactWalk.Sorted(
            ImpactWalk.Bfs(cx.Host, key, name, depth).Where(r => filter.PathOk(r.Path))
        );
        if (rows.Count == 0)
        {
            var none = Ambiguity.Flag(defs.Count, $"nothing reaches {name}{filter.ScopeNote()}");
            return Stale.With(cx, key, WithChanges(none, changes));
        }

        // The cap keeps the head, so the other-definitions line leads.
        var text = OtherDefs(name, ranked.Others);
        var mark = defs.Count > 1;
        var around = (mark ? Ambiguity.Banner(1) + Ambiguity.MarkLines(text) : text) + changes;
        var config = Indexer.Config(cx);
        var budget = new Blast.Budget(
            (int)Math.Min(config.ImpactTokens, int.MaxValue),
            cx.Host.Estimate(around, TextClass.Code),
            filter.All,
            mark
        );
        // File ranks arrive with the repo map (T26); until then references order the cut.
        text += Blast.Render(
            cx,
            rows,
            name,
            budget,
            () => new Dictionary<string, float>(StringComparer.Ordinal)
        );
        var capped = Cap.Apply(cx, Ambiguity.Flag(defs.Count, text));
        return Stale.With(cx, key, WithChanges(capped, changes));
    }

    /// <summary>The definitions of one file as <c>read</c>'s map mode prints them, narrowed to
    /// one <paramref name="kind"/> when given.</summary>
    public static string Outline(Ctx cx, string root, string path, string kind = "")
    {
        ArgumentNullException.ThrowIfNull(cx);
        ArgumentNullException.ThrowIfNull(kind);
        var text = FileOutline.Map(root, path);
        var key = GraphRoot.TryCheck(root);
        var capped = Cap.Apply(cx, text);
        var answer = key is null ? capped : Stale.With(cx, key, capped);
        return FileOutline.OfKind(answer, path, kind);
    }

    /// <summary>A code question answered in one call: the query's names with their bodies,
    /// the call paths between them, and how many definitions reach each.</summary>
    public static string Explore(Ctx cx, string root, string query, GraphFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(cx);
        filter ??= GraphFilter.None;
        var key = Stale.IndexFor(cx, root);
        var parts = new TagsExplore(cx, key, filter);
        var names = ExploreAnswer.Names(query, parts);
        // No name resolved: the signatures and docs are searched too.
        if (names.Count == 0)
        {
            foreach (var hit in cx.Host.SymbolFts(key, query, ExploreAnswer.MaxNames))
            {
                if (!names.Exists(n => string.Equals(n.Name, hit.Name, StringComparison.Ordinal)))
                {
                    names.Add((hit.Name, ""));
                }
            }
        }

        var (text, before) = ExploreAnswer.Assemble(query, filter, parts, names);
        return Stale.With(cx, key, Cap.Apply(cx, text, before, "explore"));
    }

    internal static string OtherDefs(string name, int others) =>
        others > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"+{others} other definitions of {name}\n"
            )
            : "";

    // `impact --to`: the stored paths walk callers, so the arrows are reversed.
    private static string Chains(Ctx cx, string key, string name, string to, int depth, int defs)
    {
        var chains = cx
            .Host.SymbolPaths(key, to, name, Math.Clamp(depth, 1, ImpactWalk.MaxDepth))
            .Select(c => string.Join(" → ", c.Split(" → ").Reverse()))
            .ToList();
        if (chains.Count == 0)
        {
            return Stale.With(
                cx,
                key,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no path from {name} to {to} within depth {depth}"
                )
            );
        }

        var body = string.Join('\n', chains) + "\n";
        return Stale.With(cx, key, Cap.Apply(cx, Ambiguity.Flag(defs, body)));
    }

    // The co-change line goes after the cap, which keeps the head: a long walk cannot hide it.
    private static string WithChanges(string text, string? changes) =>
        changes is null ? text : (text.EndsWith('\n') ? text : text + "\n") + changes + "\n";

    /// <summary>The tags index's answers to <see cref="IExploreParts"/>.</summary>
    private sealed class TagsExplore(Ctx cx, string key, GraphFilter filter) : IExploreParts
    {
        public IReadOnlyList<string> Resolve(string token) =>
            cx.Host.SymbolDefs(key, token).Count > 0
                ? [token]
                : cx.Host.SymbolNamePrefix(key, token, 5);

        public string Defs(string name)
        {
            var ranked = Graph.Resolve.Rank(cx.Host, key, name);
            var rows = cx.Host.SymbolDefs(key, name).Where(r => filter.PathOk(r.Path)).ToList();
            if (ranked.DefPath is { } path)
            {
                var kept = rows.Where(r => string.Equals(r.Path, path, StringComparison.Ordinal))
                    .ToList();
                if (kept.Count > 0)
                {
                    rows = kept;
                }
            }

            return rows.Count == 0
                ? $"no definition of {name}{filter.ScopeNote()}\n"
                : Bodies.Text(cx, key, name, rows) + OtherDefs(name, ranked.Others);
        }

        public IReadOnlyList<string> Paths(string a, string b) => cx.Host.SymbolPaths(key, a, b, 3);

        public (string Text, int Rows) Impact1(string name)
        {
            var ranked = Graph.Resolve.Rank(cx.Host, key, name);
            var rows = ImpactWalk.Sorted(
                ImpactWalk.Bfs(cx.Host, key, name, 1).Where(r => filter.PathOk(r.Path))
            );
            return rows.Count == 0
                ? ($"nothing reaches {name}{filter.ScopeNote()}", 0)
                : (ImpactWalk.Lines(rows) + OtherDefs(name, ranked.Others), rows.Count);
        }

        public int DefCount(string name) => cx.Host.SymbolDefs(key, name).Count;
    }
}
