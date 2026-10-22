using System.Globalization;
using System.Text;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The <c>impact</c> walk under <c>impact_tokens</c> (rtok <c>blast.rs</c>). The flat
/// <c>depth  path  name</c> listing stays while it fits; past the budget the rows are grouped by
/// file, nearest and most central first, and a closing line says what was left out and how to
/// get it: <c>impact &lt;name&gt; --all</c>.
/// </summary>
internal static class Blast
{
    /// <summary>Lines of one file shown under its header; the header's count carries the
    /// rest.</summary>
    private const int LinesPerFile = 3;

    /// <summary>The rows as text. <paramref name="tokens"/> 0 or <paramref name="all"/> keeps
    /// the flat listing, as does a listing that fits. <paramref name="overhead"/> is what the
    /// caller adds around the rows, and <paramref name="mark"/> says each line will end
    /// <c> ?</c>; both come off the budget. <paramref name="rank"/> is each file's stored rank,
    /// read only when the listing is cut.</summary>
    public static string Render(
        Ctx cx,
        IReadOnlyList<ImpactRow> rows,
        string name,
        Budget budget,
        Func<IReadOnlyDictionary<string, float>> rank
    )
    {
        var flat = ImpactWalk.Lines(rows);
        int Cost(string s) =>
            cx.Host.Estimate(budget.Mark ? Ambiguity.MarkLines(s) : s, TextClass.Code);
        // An overhead past the budget still leaves room for one file: never an empty answer.
        var left = Math.Max(budget.Tokens - budget.Overhead, 1);
        if (budget.All || budget.Tokens == 0 || Cost(flat) <= left)
        {
            return flat;
        }

        var ranks = rank();
        var groups = Groups(rows, ranks);
        // The cut line is reserved first, so appending it cannot overshoot the budget.
        left = Math.Max(left - Cost(Cut(name, groups.Count, rows.Count)), 0);
        var output = new StringBuilder();
        var shown = 0;
        foreach (var group in groups)
        {
            var block = group.Render();
            var tokens = Cost(block);
            // The first file always prints: a budget below one block must not answer nothing.
            if (shown > 0 && tokens > left)
            {
                break;
            }

            left = Math.Max(left - tokens, 0);
            output.Append(block);
            shown++;
        }

        var rest = groups.Skip(shown).ToList();
        if (rest.Count > 0)
        {
            output.Append(Cut(name, rest.Count, rest.Sum(g => g.Rows.Count)));
        }

        var text = output.ToString();
        // A saving only against the listing the model would have read otherwise.
        var before = Encoding.UTF8.GetByteCount(flat);
        var after = Encoding.UTF8.GetByteCount(text);
        if (after < before)
        {
            cx.Host.Record(
                new Measurement
                {
                    Plugin = "graph",
                    Kind = "impact",
                    BeforeBytes = before,
                    AfterBytes = after,
                    EstBefore = cx.Host.Estimate(flat, TextClass.Code),
                    EstAfter = cx.Host.Estimate(text, TextClass.Code),
                    CallId = cx.Host.CallId,
                }
            );
        }

        return text;
    }

    private static string Cut(string name, int files, int refs) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"+{files} files, {refs} refs not shown — impact {name} --all\n"
        );

    // By nearest depth, then stored rank, then more references, then path.
    private static List<Group> Groups(
        IReadOnlyList<ImpactRow> rows,
        IReadOnlyDictionary<string, float> ranks
    ) =>
        [
            .. rows.GroupBy(r => r.Path, StringComparer.Ordinal)
                .Select(g => new Group(g.Key, g.Min(r => r.Depth), [.. g]))
                .OrderBy(g => g.Depth)
                .ThenByDescending(g => ranks.GetValueOrDefault(g.Path))
                .ThenByDescending(g => g.Rows.Count)
                .ThenBy(g => g.Path, StringComparer.Ordinal),
        ];

    /// <summary>What the answer may cost: <c>impact_tokens</c> (0 = no budget), the caller's
    /// text around the rows, <c>--all</c>, and whether every line is marked ambiguous.</summary>
    public readonly record struct Budget(int Tokens, int Overhead, bool All, bool Mark);

    private sealed record Group(string Path, int Depth, List<ImpactRow> Rows)
    {
        public string Render()
        {
            var n = Rows.Count;
            var output = new StringBuilder();
            output.Append(
                CultureInfo.InvariantCulture,
                $"{Path} ({n} ref{(n == 1 ? "" : "s")}, depth {Depth})\n"
            );
            foreach (var row in Rows.Take(LinesPerFile))
            {
                output.Append(
                    CultureInfo.InvariantCulture,
                    $"  {row.Depth}  {(row.Scope.Length == 0 ? "(file)" : row.Scope)}\n"
                );
            }

            if (n > LinesPerFile)
            {
                output.Append(CultureInfo.InvariantCulture, $"  +{n - LinesPerFile} more\n");
            }

            return output.ToString();
        }
    }
}
