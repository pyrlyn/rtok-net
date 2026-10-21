namespace Rtok.Config;

/// <summary>rtok doctor</summary>
/// <remarks>Read from [doctor].</remarks>
public sealed class DoctorSection
{
    /// <summary>Claude Code settings doctor reads</summary>
    public string SettingsPath { get; set; } = "~/.claude/settings.json";

    /// <summary>Claude Code's user state file</summary>
    public string ClaudeJson { get; set; } = "~/.claude.json";

    /// <summary>project MCP servers file</summary>
    public string McpJson { get; set; } = ".mcp.json";

    /// <summary>per proxy hop /health probe</summary>
    public ulong ProbeTimeoutMs { get; set; } = 500;

    /// <summary>per MCP server tools/list (uvx/npx servers start slowly)</summary>
    public ulong McpTimeoutMs { get; set; } = 15000;

    /// <summary>--instructions: flag files above this</summary>
    public uint InstructionWarnTokens { get; set; } = 1000;

    /// <summary>run the instruction audit by default (--instructions)</summary>
    public bool Instructions { get; set; }

    /// <summary>when [proxy.tools_rewrite] is suggested</summary>
    public uint ToolsRewriteMinDescTokens { get; set; } = 2000;
}
