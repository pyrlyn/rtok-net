using CsCheck;
using Rtok.Core;

namespace Rtok.Report.Tests;

/// <summary>Whatever the sections and the budget, the whole <c>--ai</c> text stays within it.</summary>
public sealed class AiBudgetTests
{
    private static readonly EstimatorRates Rates = new(3.0f, 4.0f, 2.5f, 1.5f);

    private static readonly Gen<string> Word = Gen.Char["abcxyz019 _-\n:,#"]
        .Array[1, 30]
        .Select(c => new string(c));

    private static readonly Gen<IReadOnlyList<ReportBlock>> Blocks = Gen.Select(
            Word.Array[0, 6],
            Word.Array[0, 4]
        )
        .Select(
            (lines, cells) =>
                (IReadOnlyList<ReportBlock>)
                    [
                        new Paragraph([new Span(string.Join(' ', lines))]),
                        new Table(
                            [new("k"), new("v", Numeric: true)],
                            [.. cells.Select(c => (IReadOnlyList<string>)[c, "1"])]
                        ),
                    ]
        );

    [Fact]
    public void The_text_never_exceeds_the_budget_and_drops_whole_sections()
    {
        Gen.Select(Blocks.Array[0, 6], Gen.Int[0, 600])
            .Sample(
                (blocks, budget) =>
                {
                    ReportSection[] sections =
                    [
                        .. blocks.Select((b, i) => new ReportSection($"s{i}", "S", "units: x", b)),
                    ];
                    var document = new ReportDocument("rtok report", Facts.Window, sections);
                    var renderer = new AiRenderer(budget, Rates);

                    var text = renderer.Text(document);

                    Assert.True(renderer.Tokens(text) <= budget);
                    if (text.Length == 0)
                    {
                        return;
                    }
                    Assert.StartsWith(AiRenderer.Head, text, StringComparison.Ordinal);
                    var dropped = Dropped(text);
                    // Every section is either kept whole under its anchor or named as dropped.
                    Assert.All(
                        sections,
                        s =>
                            Assert.NotEqual(
                                dropped.Contains(s.Id),
                                text.Contains($"#{s.Id}\nunits: x\n", StringComparison.Ordinal)
                            )
                    );
                },
                iter: 500
            );
    }

    [Fact]
    public void A_budget_below_the_header_degrades_to_the_dropped_list_then_to_nothing()
    {
        var document = new ReportDocument(
            "rtok report",
            Facts.Window,
            [new ReportSection("a", "A", "units: x", [new CodeBlock("body\n")])]
        );
        var minimal = AiRenderer.Head + "dropped: a\n";
        var fits = new AiRenderer(0, Rates).Tokens(minimal);

        Assert.Equal(minimal, new AiRenderer(fits, Rates).Text(document));
        Assert.Equal("", new AiRenderer(fits - 1, Rates).Text(document));
    }

    [Fact]
    public void A_big_budget_keeps_everything()
    {
        using var fixture = new ReportFixture();
        var document = fixture.Document();

        var text = new AiRenderer(1_000_000, Rates).Text(document);

        Assert.Contains("dropped: none\n", text, StringComparison.Ordinal);
        Assert.All(
            document.Sections,
            s => Assert.Contains($"#{s.Id}\n", text, StringComparison.Ordinal)
        );
    }

    private static string[] Dropped(string text)
    {
        var line = text.Split('\n').First(l => l.StartsWith("dropped: ", StringComparison.Ordinal));
        return line is "dropped: none" ? [] : line["dropped: ".Length..].Split(',');
    }
}
