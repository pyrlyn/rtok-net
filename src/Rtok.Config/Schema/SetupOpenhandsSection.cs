namespace Rtok.Config;

/// <summary>OpenHands's user-level MCP file</summary>
/// <remarks>Read from [setup.openhands].</remarks>
public sealed class SetupOpenhandsSection
{
    /// <summary>OpenHands: user-level MCP servers (mcpServers)</summary>
    public string McpPath { get; set; } = "~/.openhands/mcp.json";
}
