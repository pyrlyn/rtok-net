namespace Rtok.Report;

/// <summary>One section.</summary>
/// <param name="Id">The stable anchor: the lowercase title.</param>
/// <param name="Title">The heading.</param>
/// <param name="Units">What the numbers count, for <c>--ai</c>.</param>
/// <param name="Blocks">The content in order.</param>
public sealed record ReportSection(
    string Id,
    string Title,
    string Units,
    IReadOnlyList<ReportBlock> Blocks
);
