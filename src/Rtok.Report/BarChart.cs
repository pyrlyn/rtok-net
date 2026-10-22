namespace Rtok.Report;

/// <summary>Horizontal bars on one scale: the longest bar is the chart's largest value.</summary>
/// <param name="Title">What the chart shows.</param>
/// <param name="Bars">The bars in order.</param>
public sealed record BarChart(string Title, IReadOnlyList<Bar> Bars) : ReportBlock
{
    /// <summary>The value the full width stands for: the largest bar, at least 1.</summary>
    public long Scale => Bars.Count == 0 ? 1 : Math.Max(1, Bars.Max(b => b.Value));

    /// <summary>A bar's share of the full width, 0 to 1.</summary>
    public double Share(Bar bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        return Math.Max(bar.Value, 0) / (double)Scale;
    }
}
