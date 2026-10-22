namespace Rtok.Report;

/// <summary>Preformatted text, such as the doctor page.</summary>
/// <param name="Text">The text.</param>
public sealed record CodeBlock(string Text) : ReportBlock;
