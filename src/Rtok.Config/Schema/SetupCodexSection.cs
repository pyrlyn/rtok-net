namespace Rtok.Config;

/// <summary>Codex's `config.toml`</summary>
/// <remarks>Read from [setup.codex].</remarks>
public sealed class SetupCodexSection
{
    /// <summary>hooks and the MCP server go here</summary>
    public string ConfigPath { get; set; } = "~/.codex/config.toml";
}
