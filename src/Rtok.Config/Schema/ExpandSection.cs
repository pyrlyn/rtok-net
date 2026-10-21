namespace Rtok.Config;

/// <summary>rtok expand &lt;id&gt;</summary>
/// <remarks>Read from [expand].</remarks>
public sealed class ExpandSection
{
    /// <summary>0 = unlimited (--lines a-b is per call)</summary>
    public uint MaxLines { get; set; }

    /// <summary>re-read ceiling; above it the report flags lossy compression</summary>
    public double MaxRate { get; set; } = 0.05;
}
