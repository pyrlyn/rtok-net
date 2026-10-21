namespace Rtok.Config;

/// <summary>opt-in response cache</summary>
/// <remarks>Read from [plugins.proxy.semantic_cache].</remarks>
public sealed class SemanticCacheSection
{
    /// <summary>off until the cache passes its audit</summary>
    public bool Enabled { get; set; }

    /// <summary>cosine similarity; semantic tier only</summary>
    public double Threshold { get; set; } = 0.99;

    /// <summary>seconds a cached reply stays fresh</summary>
    public ulong TtlS { get; set; } = 300;

    /// <summary>bifrost uses 3; start stricter for agents</summary>
    public uint MaxMessages { get; set; } = 1;

    /// <summary>cache only requests without tools</summary>
    public bool RequireEmptyTools { get; set; } = true;

    /// <summary>"hash" = direct tier only until embeddings</summary>
    public string EmbedBackend { get; set; } = "hash";

    /// <summary>the model is part of the cache key</summary>
    public bool CacheByModel { get; set; } = true;

    /// <summary>the provider is part of the cache key</summary>
    public bool CacheByProvider { get; set; } = true;
}
