namespace Rtok.Config;

/// <summary>sync scripts; the agent lane follows the global switches, batch and files are never rewritten</summary>
/// <remarks>Read from [proxy.lanes.bulk], [proxy.lanes.embeddings], [proxy.lanes.internal], [proxy.lanes.meta].</remarks>
public sealed class LanePolicy
{
    /// <summary>proxy.mode = "compress" rewrites (archive, compress, noise strip)</summary>
    public bool Compress { get; set; }

    /// <summary>the toon filter inside that pass; needs compress</summary>
    public bool Toon { get; set; }

    /// <summary>proxy.tools_rewrite</summary>
    public bool ToolsRewrite { get; set; }

    /// <summary>proxy.context_management</summary>
    public bool ContextManagement { get; set; }

    /// <summary>plugins.proxy.semantic_cache, lookup and store</summary>
    public bool SemanticCache { get; set; }

    /// <summary>OpenAI service_tier = "flex" on this lane; see [proxy.flex]</summary>
    public bool Flex { get; set; }

    /// <summary>read timeout for this lane; 0 = proxy.timeout_s, but 900 or more with flex = true (OpenAI's Flex guide uses 15 min)</summary>
    public ulong TimeoutS { get; set; }

    /// <summary>base URL for every request on this lane, any wire; "" = proxy.upstream / openai_upstream / gemini_upstream</summary>
    public string Upstream { get; set; } = "";

    /// <summary>requests upstream at once; 0 = no cap (the agent lane is never capped)</summary>
    public uint MaxInFlight { get; set; }

    /// <summary>with max_in_flight: requests that wait for a slot; one more gets 429 + Retry-After</summary>
    public uint MaxQueued { get; set; } = 8;
}
