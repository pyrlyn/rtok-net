using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// Which of several same-named definitions a reference means (rtok <c>resolve.rs</c>): import
/// evidence first, then the same directory. A name referenced by more than ~5 % of the files,
/// or a tie in the best class, stays unresolved: one name has one IDF, so IDF breaks no tie
/// between its own definitions.
/// </summary>
internal static class Resolve
{
    private const int Imported = 0;
    private const int SameDir = 1;
    private const int NoEvidence = 2;

    public static Ranked Rank(ISymbols symbols, string root, string name)
    {
        var defs = symbols.SymbolDefs(root, name);
        if (defs.Count < 2)
        {
            return Ranked.Unresolved;
        }

        var (df, files) = symbols.SymbolNameFreq(root, name);
        if (TooCommon(df, files))
        {
            return Ranked.Unresolved;
        }

        var imported = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (refPath, defPath) in symbols.SymbolImportedDefs(root, name))
        {
            if (!imported.TryGetValue(refPath, out var set))
            {
                imported[refPath] = set = new HashSet<string>(StringComparer.Ordinal);
            }

            set.Add(defPath);
        }

        var winners = new Dictionary<string, (int Class, HashSet<string> Files)>(
            StringComparer.Ordinal
        );
        foreach (var group in symbols.SymbolRefGroups(root, name))
        {
            if (Winner(group.Path, defs, imported) is not var (def, cls))
            {
                continue;
            }

            (int Class, HashSet<string> Files) cand = winners.GetValueOrDefault(
                def,
                (NoEvidence + 1, new HashSet<string>(StringComparer.Ordinal))
            );
            cand.Files.Add(group.Path);
            winners[def] = (Math.Min(cand.Class, cls), cand.Files);
        }

        if (winners.Count == 0)
        {
            return Ranked.Unresolved;
        }

        var best = winners.Values.Min(c => c.Class);
        var top = winners.Where(w => w.Value.Class == best).ToList();
        return top.Count != 1
            ? Ranked.Unresolved
            : new Ranked(top[0].Value.Files, defs.Count - 1, top[0].Key);
    }

    /// <summary>More than ~5 % of indexed files reference the name.</summary>
    internal static bool TooCommon(long df, long files) => files > 0 && df * 20 > files;

    // The one definition with the best evidence for a reference in `path`; none on a tie or
    // without evidence.
    private static (string Def, int Class)? Winner(
        string path,
        IReadOnlyList<(string Path, string Kind, int Line, int EndLine)> defs,
        Dictionary<string, HashSet<string>> imported
    )
    {
        var best = new List<string>();
        var cls = NoEvidence + 1;
        foreach (var def in defs)
        {
            var c =
                imported.TryGetValue(path, out var set) && set.Contains(def.Path) ? Imported
                : string.Equals(Dir(path), Dir(def.Path), StringComparison.Ordinal) ? SameDir
                : NoEvidence;
            if (c < cls)
            {
                cls = c;
                best.Clear();
            }

            if (c == cls)
            {
                best.Add(def.Path);
            }
        }

        return cls >= NoEvidence || best.Count != 1 ? null : (best[0], cls);
    }

    private static string Dir(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }
}
