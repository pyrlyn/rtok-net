namespace Rtok.Config;

/// <summary>Junie CLI's user `mcp.json`</summary>
/// <remarks>Read from [setup.junie].</remarks>
public sealed class SetupJunieSection
{
    /// <summary>empty: ~/.junie/mcp/mcp.json (Junie CLI user MCP file)</summary>
    public string McpPath { get; set; } = "";
}
