namespace Rtok.Config;

/// <summary>CodeBuddy Code's config directory: `settings.json` (`hooks`), the MCP file, `plugins/`</summary>
/// <remarks>Read from [setup.codebuddy].</remarks>
public sealed class SetupCodebuddySection
{
    /// <summary>CodeBuddy Code: settings.json (hooks), .mcp.json, plugins/; CODEBUDDY_CONFIG_DIR moves it</summary>
    public string Dir { get; set; } = "~/.codebuddy";

    /// <summary>WorkBuddy Desktop: the same files (embedded CodeBuddy Code; unverified)</summary>
    public string WorkbuddyDir { get; set; } = "~/.workbuddy";
}
