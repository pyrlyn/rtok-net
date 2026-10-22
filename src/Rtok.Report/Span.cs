namespace Rtok.Report;

/// <summary>A run of text in a <see cref="Paragraph"/>.</summary>
/// <param name="Text">The text.</param>
/// <param name="Kind">How it is set.</param>
public sealed record Span(string Text, SpanKind Kind = SpanKind.Text);
