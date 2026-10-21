namespace Rtok.Config;

/// <summary>MiMo Code's `mimocode.json` (`mcp`), the OpenCode-fork config file; `MIMOCODE_HOME` or `MIMOCODE_CONFIG` moves it</summary>
/// <remarks>Read from [setup.mimo].</remarks>
public sealed class SetupMimoSection
{
    /// <summary>mcp (OpenCode-fork shape)</summary>
    public string ConfigPath { get; set; } = "~/.config/mimocode/mimocode.json";
}
