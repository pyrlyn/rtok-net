namespace Rtok.Agents.Usage;

/// <summary>One agent's row.</summary>
/// <param name="Host">The host id, or <c>unattributed (&lt;api&gt;)</c>.</param>
/// <param name="Name">The name a person knows the host by.</param>
/// <param name="Row">Its legs and cost.</param>
/// <param name="ThroughRtokTokens"><c>both</c> only: its tokens that also passed through rtok.</param>
/// <param name="Coverage">
/// <c>both</c> only: <paramref name="ThroughRtokTokens"/> over the logs' tokens, so an agent that
/// bypasses the proxy reads low; null when the logs hold no tokens for it.
/// </param>
/// <param name="Saved"><c>rtok</c> and <c>both</c>: what rtok saved this agent.</param>
public sealed record UsageAgent(
    string Host,
    string Name,
    UsageRow Row,
    long? ThroughRtokTokens,
    double? Coverage,
    UsageSaved? Saved
);
