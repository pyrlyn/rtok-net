namespace Rtok.Config;

/// <summary>CodeWhale's `config.toml` (`[[hooks.hooks]]`) and sibling `mcp.json` live under `dir`; `$CODEWHALE_HOME` moves it</summary>
/// <remarks>Read from [setup.codewhale].</remarks>
public sealed class SetupCodewhaleSection
{
    /// <summary>config.toml ([[hooks.hooks]]), mcp.json (mcpServers)</summary>
    public string Dir { get; set; } = "~/.codewhale";
}
