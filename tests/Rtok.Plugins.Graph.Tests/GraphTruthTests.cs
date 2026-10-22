using System.Diagnostics;
using System.Text.Json;
using Rtok.Plugin.Sdk;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Plugins.Graph.Tests;

// rtok tests/graph_truth.rs: this repository's own C#, labelled from a plain-text scan in
// tests/fixtures/graph_truth.json, against a cold index of the repository.
public sealed class GraphTruthTests(ITestOutputHelper output)
{
    private sealed record Truth(string Name, string[] Defs, string[] Refs);

    private static List<Truth> Labels()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RepoPaths.Fixture("graph_truth.json")));
        static string[] List(JsonElement e, string key) =>
            [.. e.GetProperty(key).EnumerateArray().Select(v => v.GetString()!)];
        return
        [
            .. doc
                .RootElement.GetProperty("symbols")
                .EnumerateArray()
                .Select(e => new Truth(
                    e.GetProperty("name").GetString()!,
                    List(e, "defs"),
                    List(e, "refs")
                )),
        ];
    }

    // `name` as a whole identifier outside `//` lines, the rule the labels were scanned with.
    private static bool Mentions(string src, string name) =>
        src.Split('\n')
            .Any(l =>
            {
                if (l.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    return false;
                }

                for (
                    var i = l.IndexOf(name, StringComparison.Ordinal);
                    i >= 0;
                    i = l.IndexOf(name, i + 1, StringComparison.Ordinal)
                )
                {
                    var end = i + name.Length;
                    if ((i == 0 || !IsIdent(l[i - 1])) && (end == l.Length || !IsIdent(l[end])))
                    {
                        return true;
                    }
                }

                return false;
            });

    private static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool Backed(string name, string file)
    {
        var path = Path.Join(RepoPaths.Root, file);
        return File.Exists(path) && Mentions(File.ReadAllText(path), name);
    }

    // Other lanes move code under the labels: a file that no longer mentions its symbol is
    // skipped, not scored as an index miss, but a corpus mostly gone stale fails here.
    [Fact]
    public void Most_labels_are_still_backed_by_the_tree()
    {
        var labels = Labels().SelectMany(t => t.Defs.Concat(t.Refs).Select(f => (t.Name, f)));
        var all = labels.ToList();
        var stale = all.Where(l => !Backed(l.Name, l.f)).Select(l => $"{l.Name} {l.f}").ToList();

        Assert.True(
            stale.Count * 10 <= all.Count,
            "most labels are stale; relabel tests/fixtures/graph_truth.json and say why in its "
                + "note: "
                + string.Join(", ", stale)
        );
    }

    // Definitions clear rtok's P8b bar of 0.9 with precision 0.99, and recall clears 0.92. The
    // reference floor guards the measured 1.000 (c_sharp/extra.scm; 0.000 on the definitions-only
    // query rtok ships), with slack for label drift.
    [Fact]
    public void Labelled_symbols_are_found()
    {
        using var data = new TempDir("rtok-graph-truth-");
        using var store = RtokStore.Open(
            new StoreOptions(data.Combine("rtok.db")) { ArchiveDir = data.Combine("archive") }
        );
        var root = RepoPaths.Root;
        var cx = new Ctx(new StoreHost(store) { Cwd = root });
        var clock = Stopwatch.StartNew();
        var cold = Indexer.Run(cx, root);
        output.WriteLine($"index: cold {cold.Indexed} files in {clock.Elapsed}");
        var key = GraphRoot.Check(root);

        var labels = Labels();
        var score = new Score([.. labels.SelectMany(t => t.Defs.Concat(t.Refs))]);
        foreach (var label in labels)
        {
            var t = label with
            {
                Defs = [.. label.Defs.Where(f => Backed(label.Name, f))],
                Refs = [.. label.Refs.Where(f => Backed(label.Name, f))],
            };
            score.Add(
                t,
                [.. store.SymbolDefs(key, t.Name).Select(d => d.Path)],
                [.. store.SymbolRefGroups(key, t.Name).Select(r => r.Path)]
            );
        }

        var (defRecall, refRecall, recall, precision) = score.Ratios();
        var overall = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        output.WriteLine(
            $"graph_truth: precision {precision:F3} recall {recall:F3} overall {overall:F3}; "
                + $"definitions {score.DefGot}/{score.DefWant}, "
                + $"references {score.RefGot}/{score.RefWant} ({refRecall:F3})"
        );
        foreach (var m in score.Misses)
        {
            output.WriteLine("  miss: " + m);
        }

        var misses = string.Join(", ", score.Misses);
        Assert.True(defRecall >= 0.9, $"definition recall {defRecall:F3}: {misses}");
        Assert.True(precision >= 0.99, $"definition precision {precision:F3}: {misses}");
        Assert.True(refRecall >= 0.98, $"reference recall {refRecall:F3}: {misses}");
        Assert.True(recall >= 0.92, $"recall {recall:F3}: {misses}");
        Assert.True(overall >= 0.92, $"overall {overall:F3}");
    }

    // Precision counts definitions in files the corpus knows: a file added since the labels
    // were taken is not judged.
    private sealed class Score(HashSet<string> known)
    {
        public int DefWant { get; private set; }
        public int DefGot { get; private set; }
        public int RefWant { get; private set; }
        public int RefGot { get; private set; }
        public int Returned { get; private set; }
        public int Right { get; private set; }
        public List<string> Misses { get; } = [];

        public void Add(Truth t, HashSet<string> defs, HashSet<string> refs)
        {
            defs.IntersectWith(known);
            Returned += defs.Count;
            Right += defs.Count(t.Defs.Contains);
            Misses.AddRange(
                defs.Where(d => !t.Defs.Contains(d)).Select(d => $"extra def {t.Name} {d}")
            );
            DefWant += t.Defs.Length;
            RefWant += t.Refs.Length;
            DefGot += Tally(t.Defs, defs, $"def {t.Name}");
            RefGot += Tally(t.Refs, refs, $"ref {t.Name}");
        }

        public (double Def, double Ref, double All, double Precision) Ratios() =>
            (
                (double)DefGot / Math.Max(DefWant, 1),
                (double)RefGot / Math.Max(RefWant, 1),
                (double)(DefGot + RefGot) / Math.Max(DefWant + RefWant, 1),
                (double)Right / Math.Max(Returned, 1)
            );

        private int Tally(string[] want, HashSet<string> got, string label)
        {
            Misses.AddRange(want.Where(w => !got.Contains(w)).Select(w => $"{label} {w}"));
            return want.Count(got.Contains);
        }
    }
}
