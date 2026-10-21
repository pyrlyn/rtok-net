namespace Rtok.Config;

/// <summary>Factory Droid's user-level MCP file</summary>
/// <remarks>Read from [setup.droid].</remarks>
public sealed class SetupDroidSection
{
    /// <summary>Factory Droid: user-level MCP servers (mcpServers)</summary>
    public string McpPath { get; set; } = "~/.factory/mcp.json";
}
