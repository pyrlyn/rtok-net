using Rtok.Store;

namespace Rtok.Report.Tests;

/// <summary>Numbers at their edges, and the sink cut.</summary>
public sealed class FmtTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_non_finite_value_prints_a_dash(double value)
    {
        Assert.Equal(Fmt.NotANumber, Fmt.Dec(value));
        Assert.Equal(Fmt.NotANumber, Fmt.Percent(value));
    }

    [Fact]
    public void Counts_saturate_instead_of_wrapping()
    {
        Assert.Equal(long.MaxValue, Saturating.Sum([long.MaxValue, 1, 5]));
        Assert.Equal(long.MinValue, Saturating.Sub(long.MinValue, 1));
        Assert.Equal(long.MaxValue, Saturating.Sub(0, long.MinValue));
        Assert.Equal(int.MaxValue, Saturating.ToInt(long.MaxValue));
        Assert.Equal(0, Saturating.ToInt(double.NaN));
        var chart = new BarChart("c", [new Bar("a", long.MaxValue), new Bar("b", 1)]);
        Assert.Equal(440, HtmlRenderer.BarWidth(chart, chart.Bars[0]));
        Assert.Equal(long.MaxValue, new PluginSaving("p", 1, long.MaxValue, -5).Saved);
    }

    [Fact]
    public void Sinks_are_filtered_to_read_cmd_and_mcp_before_the_top_ten_cut()
    {
        List<MeasurementInput> rows =
        [
            .. Enumerable.Range(0, 12).Select(i => Row("graph", "outline", 1_000_000 + i, $"g{i}")),
            Row("read", "full", 30, "src/a.cs"),
            Row("cmd", "rule", 20, "git:a1"),
            Row("cmd", "rule", 20, "git:a2"),
            Row("cmd", "wrap", 10, "github:search:x1"),
        ];

        var sinks = SinkRanking.Top(rows);

        Assert.Equal(
            [
                new Sink("cmd", "git", 40, 2),
                new Sink("read", "src/a.cs", 30, 1),
                new Sink("mcp", "github:search", 10, 1),
            ],
            sinks
        );
    }

    [Fact]
    public void Only_the_ten_costliest_sinks_stay()
    {
        var rows = Enumerable
            .Range(0, 15)
            .Select(i => Row("read", "full", 100 + i, $"f{i:00}"))
            .ToList();

        var sinks = SinkRanking.Top(rows);

        Assert.Equal(SinkRanking.Limit, sinks.Count);
        Assert.Equal("f14", sinks[0].Name);
        Assert.Equal("f05", sinks[^1].Name);
    }

    private static MeasurementInput Row(string plugin, string kind, long bytes, string refId) =>
        new(plugin, kind, bytes, 1, 1, 1, refId);
}
