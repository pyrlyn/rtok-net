namespace Rtok.Report;

/// <summary>One output format of the report (README N18): it renders the document it is given
/// and computes nothing.</summary>
public interface IReportRenderer
{
    /// <summary>The format name: md, html, pdf, ai.</summary>
    string Format { get; }

    /// <summary>Writes <paramref name="document"/> to <paramref name="output"/>.</summary>
    /// <param name="document">The report.</param>
    /// <param name="output">Where the bytes go.</param>
    void Render(ReportDocument document, Stream output);
}
