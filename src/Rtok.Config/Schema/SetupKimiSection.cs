namespace Rtok.Config;

/// <summary>`mcp.json` is read beside `config_path`</summary>
/// <remarks>Read from [setup.kimi].</remarks>
public sealed class SetupKimiSection
{
    /// <summary>mcp.json is read beside it</summary>
    public string ConfigPath { get; set; } = "~/.kimi-code/config.toml";
}
