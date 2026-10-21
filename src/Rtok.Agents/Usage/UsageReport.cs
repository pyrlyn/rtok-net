namespace Rtok.Agents.Usage;

/// <summary>What <c>rtok agents usage</c> shows, in text or JSON.</summary>
/// <param name="Source"><c>logs</c>, <c>rtok</c> or <c>both</c>.</param>
/// <param name="Tz">The IANA zone the days and months are in, or <c>local</c>.</param>
/// <param name="Through">The last day with usage, in <paramref name="Tz"/>.</param>
/// <param name="Totals">The whole window.</param>
/// <param name="UnpricedModels">Distinct model ids without a price.</param>
/// <param name="Unpriced">Those models, per agent.</param>
/// <param name="Agents">One row per agent, dearest first.</param>
/// <param name="Models"><c>--by model</c> only: the rows the middle table shows instead of agents.</param>
/// <param name="Periods">One row per month, or per day with <paramref name="Daily"/>.</param>
/// <param name="Skipped">Hosts whose files exist but could not be read: named, counted nowhere.</param>
/// <param name="Daily">True for day rows.</param>
public sealed record UsageReport(
    string Source,
    string Tz,
    string? Through,
    UsageTotals Totals,
    int UnpricedModels,
    IReadOnlyList<UsageUnpriced> Unpriced,
    IReadOnlyList<UsageAgent> Agents,
    IReadOnlyList<UsageModelRow>? Models,
    IReadOnlyList<UsagePeriod> Periods,
    IReadOnlyList<UsageSkip> Skipped,
    bool Daily
);
