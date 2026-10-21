namespace Rtok.Config;

/// <summary>`mcp-config.json` and `hooks/rtok.json` live under `dir`</summary>
/// <remarks>Read from [setup.copilot].</remarks>
public sealed class SetupCopilotSection
{
    /// <summary>mcp-config.json, hooks/rtok.json</summary>
    public string Dir { get; set; } = "~/.copilot";
}
