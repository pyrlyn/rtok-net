namespace Rtok.Config;

/// <summary>rtok report (renders the operator model, computes nothing)</summary>
/// <remarks>Read from [report].</remarks>
public sealed class ReportSection
{
    /// <summary>md; html, pdf, --ai</summary>
    public string Format { get; set; } = "md";

    /// <summary>"" = stdout (--out &lt;path&gt;)</summary>
    public string Out { get; set; } = "";

    /// <summary>how far back the report reads</summary>
    public string Since { get; set; } = "30d";

    /// <summary>model-shaped rendering instead of --format (--ai)</summary>
    public bool Ai { get; set; }

    /// <summary>--ai drops whole sections past this</summary>
    public uint BudgetTokens { get; set; } = 8000;
}
