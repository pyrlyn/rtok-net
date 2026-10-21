namespace Rtok.Config;

/// <summary>Cline's hooks folder, shared by the CLI and the extension, and its MCP settings file</summary>
/// <remarks>Read from [setup.cline].</remarks>
public sealed class SetupClineSection
{
    /// <summary>Cline's hooks folder</summary>
    public string HooksPath { get; set; } = "~/Documents/Cline/Hooks";

    /// <summary>CLI; the extension uses the VS Code globalStorage settings file</summary>
    public string McpPath { get; set; } = "~/.cline/data/settings/cline_mcp_settings.json";
}
