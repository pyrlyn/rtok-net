using Rtok.Store;

namespace Rtok.Report.Tests;

/// <summary>No Measurement row, no saving claim.</summary>
public sealed class SavingsTests
{
    [Fact]
    public void Every_saving_figure_is_a_sum_of_measurement_rows()
    {
        using var fixture = new ReportFixture();
        var document = fixture.Document();
        var rows = fixture.Store.MeasurementsSince(document.Window.From.ToUnixTimeSeconds());

        var table = Assert.IsType<Table>(Savings(document).Blocks[1]);

        Assert.Equal(
            rows.GroupBy(r => r.Plugin, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                    (IReadOnlyList<string>)
                        [
                            g.Key,
                            Fmt.Num(g.Count()),
                            Fmt.Num(g.Sum(r => r.EstBefore)),
                            Fmt.Num(g.Sum(r => r.EstAfter)),
                            Fmt.Num(g.Sum(r => r.EstBefore - r.EstAfter)),
                        ]
                ),
            table.Rows
        );
    }

    [Fact]
    public void Hook_calls_without_a_measurement_claim_nothing()
    {
        using var fixture = new ReportFixture(seed: false);
        fixture.Store.UpsertSession("s1", fixture.Store.HostId("claude"), "p", "/p", "claude");
        for (var i = 0; i < 12; i++)
        {
            fixture.Store.InsertCall("s1", "hook", "hook", name: "Stop");
        }
        var document = fixture.Document();

        var savings = Savings(document);

        Assert.DoesNotContain(savings.Blocks, b => b is Table or BarChart);
        Assert.Contains(savings.Blocks, b => b is Paragraph p && p.Plain is "No rows in window.");
        var md = MarkdownRenderer.Text(document);
        Assert.Contains("| Stop | 12 | 0 |", md, StringComparison.Ordinal);
        Assert.Contains("0 tokens saved", md, StringComparison.Ordinal);
    }

    private static ReportSection Savings(ReportDocument document) =>
        document.Sections.Single(s => s.Id is "savings");
}
