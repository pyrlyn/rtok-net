namespace Rtok.Config;

/// <summary>OpenCode's `opencode.json`</summary>
/// <remarks>Read from [setup.opencode].</remarks>
public sealed class SetupOpenCodeSection
{
    /// <summary>the MCP server and plugin go here</summary>
    public string ConfigPath { get; set; } = "~/.config/opencode/opencode.json";
}
