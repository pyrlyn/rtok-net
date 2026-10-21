namespace Rtok.Agents.Usage;

/// <summary>
/// Tokens rtok saved (<c>est_before - est_after</c> over the measurements ledger, net of
/// <c>expand</c>) and their worth at the average input price the same host's requests paid. An
/// estimate, never a measurement.
/// </summary>
/// <param name="Tokens">The saved tokens.</param>
/// <param name="Usd">Their worth; null when no model of that host has a price.</param>
public sealed record UsageSaved(long Tokens, double? Usd);
