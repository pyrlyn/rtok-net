namespace Rtok.Config;

/// <summary>oh my pi: the linked pi extension and native `mcp.json`</summary>
/// <remarks>Read from [setup.omp].</remarks>
public sealed class SetupOmpSection
{
    /// <summary>oh my pi: plugins/pi is linked here</summary>
    public string ExtensionsPath { get; set; } = "~/.omp/agent/extensions";

    /// <summary>omp's MCP servers file</summary>
    public string McpPath { get; set; } = "~/.omp/agent/mcp.json";
}
