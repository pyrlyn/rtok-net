namespace Rtok.Report;

/// <summary>Running text. Every span is data: renderers escape it and add only the span's own
/// markup.</summary>
/// <param name="Spans">The spans in order.</param>
public sealed record Paragraph(IReadOnlyList<Span> Spans) : ReportBlock
{
    /// <summary>The text without markup.</summary>
    public string Plain => string.Concat(Spans.Select(s => s.Text));
}
