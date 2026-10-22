using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>A worker's result; <see cref="Sha"/> and <see cref="Rows"/> are set on
/// <see cref="ParseOutcome.Rows"/>.</summary>
internal readonly record struct Parsed(
    ParseOutcome Outcome,
    string Sha = "",
    IReadOnlyList<SymbolRow>? Rows = null
);
