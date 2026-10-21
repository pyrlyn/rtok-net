namespace Rtok.Agents.Usage;

/// <summary>One model's row, for <c>--by model</c>.</summary>
/// <param name="Model">The raw model id, or <c>unknown</c>.</param>
/// <param name="Row">Its legs and cost.</param>
public sealed record UsageModelRow(string Model, UsageRow Row);
