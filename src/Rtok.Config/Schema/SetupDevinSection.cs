namespace Rtok.Config;

/// <summary>Devin CLI and Desktop</summary>
/// <remarks>Read from [setup.devin].</remarks>
public sealed class SetupDevinSection
{
    /// <summary>mcp_config.json is read beside it; Windows: %APPDATA%\devin\</summary>
    public string ConfigPath { get; set; } = "~/.config/devin/config.json";
}
