namespace Rtok.Config;

/// <summary>Warp's user-level MCP file</summary>
/// <remarks>Read from [setup.warp].</remarks>
public sealed class SetupWarpSection
{
    /// <summary>Warp: user-level MCP servers (mcpServers)</summary>
    public string McpPath { get; set; } = "~/.warp/.mcp.json";
}
