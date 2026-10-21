namespace Rtok.Config;

/// <summary>Crush's global JSON config</summary>
/// <remarks>Read from [setup.crush].</remarks>
public sealed class SetupCrushSection
{
    /// <summary>Crush: global JSON config (mcp.rtok); CRUSH_GLOBAL_CONFIG or XDG_CONFIG_HOME move it</summary>
    public string ConfigPath { get; set; } = "~/.config/crush/crush.json";
}
