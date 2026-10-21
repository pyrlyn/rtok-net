namespace Rtok.Config;

/// <summary>Amp (Sourcegraph) user settings file (`amp.mcpServers`)</summary>
/// <remarks>Read from [setup.amp].</remarks>
public sealed class SetupAmpSection
{
    /// <summary>Amp: user settings; amp.mcpServers holds MCP servers (settings.jsonc if it is the only one)</summary>
    public string SettingsPath { get; set; } = "~/.config/amp/settings.json";
}
