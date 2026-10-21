namespace Rtok.Config;

/// <summary>summaries of archived tool output</summary>
/// <remarks>Read from [plugins.compress].</remarks>
public sealed class CompressSection
{
    /// <summary>extractive summaries of archived tool output; runs only in proxy.mode = "compress"</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The [plugins.compress.llm] table.</summary>
    public CompressLlmSection Llm { get; set; } = new();
}
