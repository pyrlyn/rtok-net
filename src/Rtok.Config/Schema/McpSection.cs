namespace Rtok.Config;

/// <summary>rtok mcp</summary>
/// <remarks>Read from [mcp].</remarks>
public sealed class McpSection
{
    /// <summary>[] = all tools from enabled plugins; else an allow-list; `expand` always stays listed</summary>
    public IList<string> Tools { get; set; } = [];

    /// <summary>enforced by a test</summary>
    public uint MaxDescriptionTokens { get; set; } = 60;

    /// <summary>above this, head/tail + archive id</summary>
    public uint MaxResultChars { get; set; } = 20000;

    /// <summary>`rtok mcp --http` with no address; keep it on loopback</summary>
    public string Http { get; set; } = "127.0.0.1:8791";

    /// <summary>HTTP allow-list, used instead of `tools`; `expand` always stays listed; tools that act as the calling agent (`whoami`, `agent_send`, `worktree_add`, …) never are</summary>
    public IList<string> HttpTools { get; set; } = ["read", "search", "tree"];

    /// <summary>bearer token for --http; prefer RTOK_MCP_TOKEN; empty = --http refuses to start</summary>
    public string Token { get; set; } = "";

    /// <summary>tunnel URL (https://…); its host and origin are the only foreign ones accepted</summary>
    public string PublicUrl { get; set; } = "";
}
