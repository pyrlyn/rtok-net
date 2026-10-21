namespace Rtok.Config;

/// <summary>Kiro IDE and CLI user-level MCP file</summary>
/// <remarks>Read from [setup.kiro].</remarks>
public sealed class SetupKiroSection
{
    /// <summary>Kiro IDE and CLI: user-level MCP servers (mcpServers)</summary>
    public string McpPath { get; set; } = "~/.kiro/settings/mcp.json";
}
