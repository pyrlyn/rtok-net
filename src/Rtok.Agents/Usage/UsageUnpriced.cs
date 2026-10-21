namespace Rtok.Agents.Usage;

/// <summary>A model the estimate leaves out, per agent.</summary>
/// <param name="Model">The raw model id.</param>
/// <param name="Host">The agent that used it.</param>
/// <param name="Tokens">Its tokens, counted in every total but in no cost.</param>
public sealed record UsageUnpriced(string Model, string Host, long Tokens);
