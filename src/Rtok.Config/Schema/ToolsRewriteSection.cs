namespace Rtok.Config;

/// <summary>off: request bytes stay identical</summary>
/// <remarks>Read from [proxy.tools_rewrite].</remarks>
public sealed class ToolsRewriteSection
{
    /// <summary>rewrite tools[] in proxied requests</summary>
    public bool Enabled { get; set; }

    /// <summary>also first-sentence property descriptions + redundant default/examples inside input_schema; originals archived (Measurement.ref_id)</summary>
    public bool CompactSchema { get; set; }

    /// <summary>0 = no truncate; sentence boundary; estimator Class::Prose</summary>
    public uint MaxDescriptionTokens { get; set; } = 60;

    /// <summary>empty = keep all names not in deny</summary>
    public IList<string> Allow { get; set; } = [];

    /// <summary>drop these names from tools[]; later calls still forward</summary>
    public IList<string> Deny { get; set; } = [];
}
