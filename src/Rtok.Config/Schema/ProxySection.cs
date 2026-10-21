namespace Rtok.Config;

/// <summary>rtok proxy</summary>
/// <remarks>Read from [proxy].</remarks>
public sealed class ProxySection
{
    /// <summary>false = plain reverse proxy (bypass compress/bookkeeping); does NOT stop HTTP</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>address the proxy listens on</summary>
    public string Bind { get; set; } = "127.0.0.1";

    /// <summary>port the proxy listens on (1-65535)</summary>
    public ushort Port { get; set; } = 8790;

    /// <summary>passthrough | compress</summary>
    public string Mode { get; set; } = "passthrough";

    /// <summary>RTOK_UPSTREAM; chain behind another proxy for A/B</summary>
    public string Upstream { get; set; } = "https://api.anthropic.com";

    /// <summary>RTOK_OPENAI_UPSTREAM</summary>
    public string OpenaiUpstream { get; set; } = "https://api.openai.com";

    /// <summary>RTOK_GEMINI_UPSTREAM</summary>
    public string GeminiUpstream { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>upstream request timeout</summary>
    public ulong TimeoutS { get; set; } = 600;

    /// <summary>OpenAI streaming: add stream_options.include_usage when missing</summary>
    public bool IncludeUsage { get; set; } = true;

    /// <summary>Anthropic /v1/messages only: add clear_tool_uses edit + beta header (opt-in)</summary>
    public bool ContextManagement { get; set; }

    /// <summary>--dry-run: print effective [proxy] settings and exit, don't serve</summary>
    public bool DryRun { get; set; }

    /// <summary>The [proxy.tools_rewrite] table.</summary>
    public ToolsRewriteSection ToolsRewrite { get; set; } = new();

    /// <summary>The [proxy.lanes] table.</summary>
    public LanesSection Lanes { get; set; } = new();

    /// <summary>The [proxy.batch] table.</summary>
    public BatchPolicy Batch { get; set; } = new();

    /// <summary>The [proxy.flex] table.</summary>
    public FlexPolicy Flex { get; set; } = new();

    /// <summary>The [proxy.routing] table.</summary>
    public RoutingPolicy Routing { get; set; } = new();
}
