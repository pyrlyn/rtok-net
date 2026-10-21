namespace Rtok.Config;

/// <summary>compressor, off by default</summary>
/// <remarks>Read from [plugins.compress.llm].</remarks>
public sealed class CompressLlmSection
{
    /// <summary>model summaries of archived output via the internal lane; needs proxy.mode = "compress"</summary>
    public bool Enabled { get; set; }

    /// <summary>where the compressor posts; empty = this proxy (proxy.bind:proxy.port)</summary>
    public string Url { get; set; } = "";

    /// <summary>model that writes the summaries</summary>
    public string Model { get; set; } = "claude-haiku-4-5";

    /// <summary>larger archived results keep the extractive view</summary>
    public ulong MaxInputBytes { get; set; } = 65536;

    /// <summary>summary length cap</summary>
    public uint MaxOutputTokens { get; set; } = 512;

    /// <summary>more must-keep spans than this keep the extractive view</summary>
    public uint MaxSpans { get; set; } = 64;

    /// <summary>compressor calls at once</summary>
    public uint MaxInFlight { get; set; } = 2;

    /// <summary>seconds per compressor call</summary>
    public ulong TimeoutS { get; set; } = 60;

    /// <summary>also summarise diffs and source; off = logs and prose only</summary>
    public bool Code { get; set; }
}
