namespace Rtok.Agents.Usage;

/// <summary>The whole window.</summary>
/// <param name="Row">Every request's legs and cost.</param>
/// <param name="Sessions">Distinct session ids.</param>
/// <param name="DailyRows">Distinct (agent, day) pairs with usage, what ccusage calls daily rows.</param>
/// <param name="Saved"><c>rtok</c> and <c>both</c>: what the ledger says rtok removed.</param>
public sealed record UsageTotals(UsageRow Row, int Sessions, int DailyRows, UsageSaved? Saved);
