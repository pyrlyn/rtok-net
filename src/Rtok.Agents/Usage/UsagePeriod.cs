namespace Rtok.Agents.Usage;

/// <summary>One month (<c>2026-10</c>) or day (<c>2026-10-02</c>) in the report's zone.</summary>
/// <param name="Period">The month or day.</param>
/// <param name="Row">Its legs and cost.</param>
public sealed record UsagePeriod(string Period, UsageRow Row);
