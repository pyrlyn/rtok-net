using Rtok.Config;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>The proxy's archive pointers in the window and what expanding them cost.</summary>
public sealed record ExpandFacts(
    long Decisions,
    long Expanded,
    long Cost,
    long CostRows,
    IReadOnlyList<string> ExpandedIds
)
{
    /// <summary>expanded / decisions; 0 with no decisions.</summary>
    public double Rate => Decisions == 0 ? 0 : Expanded / (double)Decisions;
}
