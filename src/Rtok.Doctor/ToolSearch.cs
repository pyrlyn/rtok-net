using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>Claude Code's tool search state.</summary>
/// <param name="State"><c>enabled</c>, <c>disabled</c> or <c>unknown</c>.</param>
/// <param name="Source">What decided it; null for the default.</param>
public sealed record ToolSearch(string State, string? Source);
