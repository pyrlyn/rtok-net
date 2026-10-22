namespace Rtok.Measure;

/// <summary>What <c>rtok stats</c> was asked for beyond the <c>[stats]</c> config.</summary>
public sealed record StatsRequest
{
    /// <summary>Save the report's JSON under this name instead of printing it.</summary>
    public string? SaveBaseline { get; init; }

    /// <summary>Print deltas against this saved baseline; <c>stats.baseline</c> when null.</summary>
    public string? Compare { get; init; }

    /// <summary>Prompt-cache health per session instead of the report.</summary>
    public bool Cache { get; init; }
}
