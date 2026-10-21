namespace Rtok.Config;

/// <summary>the usage-capture plugin, not the `[proxy]` server</summary>
/// <remarks>Read from [plugins.proxy].</remarks>
public sealed class ProxyPluginSection
{
    /// <summary>the proxy plugin (usage capture); the server itself is [proxy]</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The [plugins.proxy.semantic_cache] table.</summary>
    public SemanticCacheSection SemanticCache { get; set; } = new();
}
