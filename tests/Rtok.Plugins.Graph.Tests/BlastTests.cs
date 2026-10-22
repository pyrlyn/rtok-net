using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph.Tests;

// rtok graph/blast.rs: impact under impact_tokens, grouped by file past the budget.
public sealed class BlastTests
{
    private static readonly Func<IReadOnlyDictionary<string, float>> NoRank = () =>
        new Dictionary<string, float>(StringComparer.Ordinal);

    // `files` callers of a hub, each with `per` definitions, all at depth 1.
    private static List<ImpactRow> Hub(int files, int per) =>
        [
            .. Enumerable
                .Range(0, files)
                .SelectMany(f =>
                    Enumerable
                        .Range(0, per)
                        .Select(d => new ImpactRow(1, $"src/f{f:000}.rs", $"fn_{f}_{d}"))
                ),
        ];

    private static string Text(Ctx cx, List<ImpactRow> rows, int tokens, bool all = false) =>
        Blast.Render(cx, rows, "hub", new Blast.Budget(tokens, 0, all, false), NoRank);

    private static int Count(string text, string part) =>
        (text.Length - text.Replace(part, "", StringComparison.Ordinal).Length) / part.Length;

    [Fact]
    public void A_small_walk_is_the_flat_listing()
    {
        using var h = new GraphHarness();
        var rows = Hub(3, 2);
        Assert.Equal(ImpactWalk.Lines(rows), Text(h.Cx, rows, 1500));
    }

    [Fact]
    public void All_and_a_zero_budget_keep_the_flat_listing()
    {
        using var h = new GraphHarness();
        var rows = Hub(200, 5);
        var flat = ImpactWalk.Lines(rows);
        Assert.Equal(flat, Text(h.Cx, rows, 300, all: true));
        Assert.Equal(flat, Text(h.Cx, rows, 0));
        Assert.Empty(h.Host.Memory.Recorded());
    }

    [Fact]
    public void A_hub_fits_the_budget_and_ends_with_the_cut_line()
    {
        using var h = new GraphHarness();
        var rows = Hub(200, 5);

        var output = Text(h.Cx, rows, 300);

        Assert.True(h.Host.Estimate(output, TextClass.Code) <= 300, output);
        var shown = Count(output, " refs, depth 1)\n");
        Assert.InRange(shown, 1, 199);
        Assert.EndsWith(
            $"+{200 - shown} files, {(200 - shown) * 5} refs not shown — impact hub --all\n",
            output,
            StringComparison.Ordinal
        );
        Assert.StartsWith(
            "src/f000.rs (5 refs, depth 1)\n  1  fn_0_0\n  1  fn_0_1\n  1  fn_0_2\n  +2 more\n",
            output,
            StringComparison.Ordinal
        );
        var row = Assert.Single(h.Host.Memory.Recorded());
        Assert.Equal("impact", row.Kind);
        Assert.True(row.AfterBytes < row.BeforeBytes);
    }

    [Fact]
    public void Files_lead_by_depth_then_rank_then_reference_count()
    {
        using var h = new GraphHarness();
        List<ImpactRow> rows =
        [
            new(2, "near_but_deep.rs", "a"),
            new(1, "many.rs", "a"),
            new(1, "many.rs", "b"),
            new(1, "central.rs", "a"),
            new(1, "few.rs", "a"),
            .. Hub(60, 1).Select(r => r with { Depth = 3, Path = "z/" + r.Path }),
        ];
        List<string> Heads(Dictionary<string, float> rank) =>
            [
                .. Blast
                    .Render(h.Cx, rows, "hub", new Blast.Budget(120, 0, false, false), () => rank)
                    .Split('\n')
                    .Where(l => l.Contains(" (", StringComparison.Ordinal) && l.EndsWith(')'))
                    .Select(l => l.Split(' ')[0])
                    .Take(4),
            ];

        Assert.Equal(
            ["many.rs", "central.rs", "few.rs", "near_but_deep.rs"],
            Heads(new Dictionary<string, float>(StringComparer.Ordinal))
        );
        Assert.Equal(
            ["few.rs", "central.rs", "many.rs", "near_but_deep.rs"],
            Heads(
                new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    ["few.rs"] = 0.5f,
                    ["central.rs"] = 0.1f,
                }
            )
        );
    }

    [Fact]
    public void Marks_and_overhead_come_off_the_budget()
    {
        using var h = new GraphHarness();
        var rows = Hub(200, 5);

        var marked = Blast.Render(
            h.Cx,
            rows,
            "hub",
            new Blast.Budget(300, 40, false, true),
            NoRank
        );
        var framed = new string('x', 40 * 3) + Ambiguity.MarkLines(marked);
        Assert.True(h.Host.Estimate(framed, TextClass.Code) <= 300, framed);

        // An overhead past the budget still prints one file and the cut line.
        var over = Blast.Render(
            h.Cx,
            rows,
            "hub",
            new Blast.Budget(300, 900, false, false),
            NoRank
        );
        Assert.Equal(1, Count(over, " refs, depth 1)\n"));
        Assert.Contains("+199 files, 995 refs not shown", over, StringComparison.Ordinal);
    }

    [Fact]
    public void Impact_on_an_indexed_hub_is_budgeted_and_all_lifts_it()
    {
        using var h = new GraphHarness("""{"impact_tokens":200}""");
        h.Write("hub.rs", "pub fn hub() {}\n");
        for (var f = 0; f < 60; f++)
        {
            h.Write(
                $"c{f:00}.rs",
                string.Concat(
                    Enumerable
                        .Range(0, 4)
                        .Select(d => $"pub fn caller_{f}_{d}() {{\n    hub();\n}}\n")
                )
            );
        }

        var output = GraphTools.Impact(h.Cx, h.Project.Path, "hub", 1);

        Assert.True(h.Host.Estimate(output, TextClass.Code) <= 200, output);
        Assert.Contains("c00.rs (4 refs, depth 1)\n", output, StringComparison.Ordinal);
        Assert.Contains(" refs not shown — impact hub --all\n", output, StringComparison.Ordinal);
        var full = GraphTools.Impact(h.Cx, h.Project.Path, "hub", 1, new GraphFilter(All: true));
        Assert.Equal(240, Count(full, "caller_"));
        Assert.DoesNotContain("not shown", full, StringComparison.Ordinal);
    }
}
