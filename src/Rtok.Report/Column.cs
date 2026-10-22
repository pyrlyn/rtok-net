namespace Rtok.Report;

/// <summary>A table column.</summary>
/// <param name="Name">The header.</param>
/// <param name="Numeric">Whether the cells are numbers, right-aligned.</param>
public sealed record Column(string Name, bool Numeric = false);
