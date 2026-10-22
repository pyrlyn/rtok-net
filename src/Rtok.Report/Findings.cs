namespace Rtok.Report;

/// <summary>Recommendations, most recoverable tokens first; empty prints
/// <see cref="Findings.None"/>.</summary>
/// <param name="Items">The findings.</param>
public sealed record Findings(IReadOnlyList<Finding> Items) : ReportBlock
{
    /// <summary>What an empty list prints.</summary>
    public const string None = "No recommendations.";
}
