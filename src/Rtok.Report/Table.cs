namespace Rtok.Report;

/// <summary>A table; every cell is data.</summary>
/// <param name="Columns">The columns.</param>
/// <param name="Rows">The rows, one cell per column.</param>
public sealed record Table(IReadOnlyList<Column> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)
    : ReportBlock;
