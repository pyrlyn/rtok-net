namespace Rtok.Report;

/// <summary>The report built once (README N18): every renderer reads this instance and computes
/// nothing of its own.</summary>
/// <param name="Title">The document title.</param>
/// <param name="Window">The window the sections cover.</param>
/// <param name="Sections">The sections in their fixed order.</param>
public sealed record ReportDocument(
    string Title,
    ReportWindow Window,
    IReadOnlyList<ReportSection> Sections
);
