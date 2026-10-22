namespace Rtok.Web.Contracts;

/// <summary>Usage totals, the context of recent turns and measured savings per day.</summary>
/// <param name="Rows">How many usage rows the totals sum.</param>
/// <param name="Turns">The context of the most recent turns, oldest first.</param>
/// <param name="Alerts">Live problems to show on top; empty when there are none.</param>
/// <param name="Savings">The recent days with measurements, oldest first.</param>
public sealed record OverviewPage(
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output,
    long Rows,
    IReadOnlyList<long> Turns,
    IReadOnlyList<string> Alerts,
    IReadOnlyList<SavingsDay> Savings
);
