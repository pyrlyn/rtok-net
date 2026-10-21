namespace Rtok.Config;

/// <summary>Claude Code's user `settings.json`</summary>
/// <remarks>Read from [setup.claude].</remarks>
public sealed class SetupClaudeSection
{
    /// <summary>hooks and the MCP server go here</summary>
    public string SettingsPath { get; set; } = "~/.claude/settings.json";
}
