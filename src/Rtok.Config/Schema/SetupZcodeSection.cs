namespace Rtok.Config;

/// <summary>Z Code's `config.json`</summary>
/// <remarks>Read from [setup.zcode].</remarks>
public sealed class SetupZcodeSection
{
    /// <summary>the MCP server goes here</summary>
    public string ConfigPath { get; set; } = "~/.zcode/cli/config.json";
}
