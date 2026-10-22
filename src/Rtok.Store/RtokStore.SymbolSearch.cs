using System.Text;
using System.Text.Json;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    // Rows FTS5 hands over before the name > signature > doc ranking cuts them to the limit.
    private const int FtsCandidates = 200;

    private IdfMemo? _idfMemo;

    /// <summary>
    /// Definitions whose name, signature or doc holds a token of <paramref name="query"/>: a
    /// name hit ranks above a signature hit, which ranks above a doc hit; then more tokens
    /// covered, then bm25, then path, line and name.
    /// </summary>
    public IReadOnlyList<(string Path, string Name, string Kind, int Line)> SymbolFts(
        string root,
        string query,
        int limit
    )
    {
        var tokens = FtsTokens(query);
        if (limit <= 0 || tokens.Count == 0)
        {
            return [];
        }

        var match = string.Join(
            " OR ",
            tokens.Select(t => $"\"{t.Replace("\"", "", StringComparison.Ordinal)}\"")
        );
        List<(
            string Path,
            string Name,
            string Kind,
            int Line,
            string Signature,
            string Doc,
            double Rank
        )> hits;
        using (var scope = Enter())
        {
            using var command = scope.Command(
                """
                SELECT s.path, s.name, s.kind, s.line, s.signature, s.doc, bm25(symbols_fts)
                FROM symbols_fts JOIN symbols s ON s.id = symbols_fts.rowid
                WHERE symbols_fts MATCH $query AND s.root = $root AND s.is_def = 1 LIMIT $limit
                """
            );
            command.Parameters.AddWithValue("$query", match);
            command.Parameters.AddWithValue("$root", root);
            command.Parameters.AddWithValue("$limit", FtsCandidates);
            hits = Rows(
                command,
                r =>
                    (
                        r.GetString(0),
                        r.GetString(1),
                        r.GetString(2),
                        r.GetInt32(3),
                        r.GetString(4),
                        r.GetString(5),
                        r.GetDouble(6)
                    )
            );
        }

        return hits.OrderBy(h => FtsTier(h.Name, h.Signature, tokens))
            .ThenByDescending(h =>
                tokens.Count(t => Has(h.Name, t) || Has(h.Signature, t) || Has(h.Doc, t))
            )
            .ThenBy(h => h.Rank)
            .ThenBy(h => h.Path, StringComparer.Ordinal)
            .ThenBy(h => h.Line)
            .ThenBy(h => h.Name, StringComparer.Ordinal)
            .Take(limit)
            .Select(h => (h.Path, h.Name, h.Kind, h.Line))
            .ToList();
    }

    /// <summary>Document frequency of <paramref name="name"/> and the number of indexed files,
    /// from the cache the last index pass wrote; <c>(0, 0)</c> before one ran.</summary>
    public (long Df, long Files) SymbolNameFreq(string root, string name)
    {
        var key = IdfKey(root);
        if (KvGet(key) is not { } body)
        {
            return (0, 0);
        }

        // Ranking asks once per visited name; the body is still read, so another process's
        // rebuild is seen, but it is parsed only when it changed.
        IdfMemo memo;
        lock (_gate)
        {
            if (
                _idfMemo is not { } m
                || !string.Equals(m.Key, key, StringComparison.Ordinal)
                || !string.Equals(m.Body, body, StringComparison.Ordinal)
            )
            {
                _idfMemo = ParseIdf(key, body);
            }

            memo = _idfMemo!;
        }

        return (memo.Df.GetValueOrDefault(name), memo.Files);
    }

    /// <summary>Recomputes the per-name count of referencing files for
    /// <see cref="SymbolNameFreq"/> and refreshes the planner statistics of <c>symbols</c>.</summary>
    public void RebuildSymbolIdf(string root)
    {
        var df = new SortedDictionary<string, long>(StringComparer.Ordinal);
        long files;
        using (var scope = Enter())
        {
            using var counts = scope.Command(
                """
                SELECT name, COUNT(DISTINCT path) FROM symbols
                WHERE root = $root AND is_def = 0 AND kind != 'import' AND name != '' GROUP BY name
                """
            );
            counts.Parameters.AddWithValue("$root", root);
            foreach (var (name, n) in Rows(counts, r => (r.GetString(0), r.GetInt64(1))))
            {
                df[name] = n;
            }

            files = Scalar(
                scope,
                "SELECT COUNT(DISTINCT path) FROM symbols WHERE root = $root",
                root
            );
            using var analyze = scope.Command("PRAGMA analysis_limit = 1000; ANALYZE symbols;");
            analyze.ExecuteNonQuery();
        }

        KvSet(IdfKey(root), IdfBody(files, df));
    }

    private static string IdfKey(string root) => "graph-idf:" + root;

    private static string IdfBody(long files, SortedDictionary<string, long> df)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("n", files);
            json.WriteStartObject("df");
            foreach (var (name, n) in df)
            {
                json.WriteNumber(name, n);
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static IdfMemo ParseIdf(string key, string body)
    {
        var df = new Dictionary<string, long>(StringComparer.Ordinal);
        long files = 0;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("n", out var n) && n.TryGetInt64(out var count))
            {
                files = count;
            }

            if (
                doc.RootElement.TryGetProperty("df", out var map)
                && map.ValueKind is JsonValueKind.Object
            )
            {
                foreach (var entry in map.EnumerateObject())
                {
                    if (entry.Value.TryGetInt64(out var v))
                    {
                        df[entry.Name] = v;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // An unreadable cache ranks as an empty one until the next index pass.
            df.Clear();
            files = 0;
        }

        return new IdfMemo(key, body, df, files);
    }

    /// <summary>The query's word tokens, each once (ASCII case folded).</summary>
    private static List<string> FtsTokens(string query)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= query.Length; i++)
        {
            var word = i < query.Length && (char.IsLetterOrDigit(query[i]) || query[i] is '_');
            if (word && start < 0)
            {
                start = i;
            }
            else if (!word && start >= 0)
            {
                var token = query[start..i];
                if (seen.Add(token))
                {
                    tokens.Add(token);
                }

                start = -1;
            }
        }

        return tokens;
    }

    private static int FtsTier(string name, string signature, List<string> tokens) =>
        tokens.Exists(t => Has(name, t)) ? 0
        : tokens.Exists(t => Has(signature, t)) ? 1
        : 2;

    private static bool Has(string hay, string token) =>
        hay.Contains(token, StringComparison.OrdinalIgnoreCase);

    private sealed record IdfMemo(string Key, string Body, Dictionary<string, long> Df, long Files);
}
