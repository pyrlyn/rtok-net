namespace Rtok.Hook.Contracts;

/// <summary>
/// The canonical hook-event table: every hook each host fires and the <c>rtok hook</c> event it
/// runs (rtok <c>agents::hook_events</c>). Installers and the plugin manifests
/// (<c>Rtok.Hooks.Events.HookManifests</c>) are written from it; row order is the order a host's config
/// lists its entries. Cursor's <c>subagentStart</c> is left out on purpose (its reply has no
/// context field), and so is <c>beforeSubmitPrompt</c> (its reply is only
/// <c>{continue, user_message}</c>).
/// </summary>
public static class HookEvents
{
    /// <summary>Every row, host by host.</summary>
    public static IReadOnlyList<HookEventRow> All { get; } =
    [
        .. ClaudeRows("claude", installer: false, subagent: true),
        // CodeBuddy's hooks page names SubagentStart without an input or output shape.
        .. ClaudeRows("codebuddy", installer: true, subagent: false),
        Row("cline", "PreToolUse", "PreToolUse"),
        Row("cline", "PostToolUse", "PostToolUse"),
        // The adapter turns TaskStart into SessionStart.
        Row("cline", "TaskStart", "TaskStart"),
        Row("cline", "UserPromptSubmit", "UserPromptSubmit"),
        Row("cline", "SessionEnd", "SessionEnd"),
        Row("commandcode", "PreToolUse", "PreToolUse", "SHELL|READ|WRITE|EDIT"),
        Row("commandcode", "PostToolUse", "PostToolUse", ".*"),
        Row("commandcode", "SessionStart", "SessionStart"),
        Row("commandcode", "Stop", "SessionEnd"),
        // Codex reads through Bash and edits with apply_patch; it has no SessionStart.
        Row("codex", "PreToolUse", "PreToolUse", "Bash|apply_patch"),
        Row("codex", "PostToolUse", "PostToolUse", "*"),
        Row("codex", "PreCompact", "PreCompact"),
        Row("codex", "PostCompact", "PostCompact"),
        Row("codex", "SubagentStart", "SubagentStart"),
        Row("copilot", "preToolUse", "PreToolUse"),
        Row("copilot", "postToolUse", "PostToolUse"),
        Row("copilot", "userPromptSubmitted", "UserPromptSubmit"),
        Row("copilot", "sessionStart", "SessionStart"),
        Row("copilot", "sessionEnd", "SessionEnd"),
        Row("copilot", "preCompact", "PreCompact"),
        Row("copilot", "subagentStart", "SubagentStart"),
        Row("cursor", "beforeShellExecution", "PreToolUse"),
        Row("cursor", "afterShellExecution", "PostToolUse"),
        Row("cursor", "sessionStart", "SessionStart", installer: false),
        Row("cursor", "sessionEnd", "SessionEnd"),
        Row("cursor", "preCompact", "PreCompact"),
        Row("cursor", "afterMCPExecution", "AfterMCPExecution", installer: false),
        Row("cursor", "postToolUse", "PostToolUse", "MCP:", installer: false),
        Row("devin", "PreToolUse", "PreToolUse", "^exec$"),
        Row("devin", "PreToolUse", "PreToolUse", "^read$"),
        Row("devin", "PostToolUse", "PostToolUse"),
        Row("devin", "UserPromptSubmit", "UserPromptSubmit"),
        Row("devin", "SessionStart", "SessionStart"),
        // The adapter turns PostCompaction into PostCompact.
        Row("devin", "PostCompaction", "PostCompaction"),
        Row("devin", "SessionEnd", "SessionEnd"),
        Row("gemini", "BeforeTool", "PreToolUse"),
        Row("gemini", "AfterTool", "PostToolUse"),
        Row("gemini", "BeforeAgent", "UserPromptSubmit"),
        Row("gemini", "SessionStart", "SessionStart"),
        Row("gemini", "SessionEnd", "SessionEnd"),
        Row("gemini", "PreCompress", "PreCompact"),
        .. ClaudeRows("qwen", installer: true, subagent: true),
        Row("grok", "PreToolUse", "PreToolUse", "Bash", installer: false),
        Row("grok", "PostToolUse", "PostToolUse", installer: false),
        Row("grok", "UserPromptSubmit", "UserPromptSubmit", installer: false),
        Row("grok", "SessionStart", "SessionStart", installer: false),
        Row("grok", "PreCompact", "PreCompact", installer: false),
        Row("grok", "PostCompact", "PostCompact", installer: false),
        Row("grok", "SessionEnd", "SessionEnd", installer: false),
        .. ClaudeRows("kimi", installer: true, subagent: false),
        Row("zcode", "PreToolUse", "PreToolUse", "Bash"),
        Row("zcode", "PreToolUse", "PreToolUse", "Read"),
        Row("zcode", "PostToolUse", "PostToolUse", "*"),
        Row("zcode", "UserPromptSubmit", "UserPromptSubmit"),
        Row("zcode", "SessionStart", "SessionStart"),
    ];

    /// <summary>The hosts with rows, in table order.</summary>
    public static IReadOnlyList<string> Hosts { get; } =
    [.. All.Select(r => r.Host).Distinct(StringComparer.Ordinal)];

    /// <summary>Every row of <paramref name="host"/>.</summary>
    public static IReadOnlyList<HookEventRow> ForHost(string host) =>
        [.. All.Where(r => string.Equals(r.Host, host, StringComparison.Ordinal))];

    /// <summary>The rows an installer writes for <paramref name="host"/>.</summary>
    public static IReadOnlyList<HookEventRow> InstallerRows(string host) =>
        [.. All.Where(r => string.Equals(r.Host, host, StringComparison.Ordinal) && r.Installer)];

    private static HookEventRow Row(
        string host,
        string hostEvent,
        string rtokEvent,
        string matcher = "",
        bool installer = true
    ) => new(host, hostEvent, rtokEvent, matcher, installer);

    // Claude's own rows, which the Claude-shaped hosts share.
    private static IEnumerable<HookEventRow> ClaudeRows(string host, bool installer, bool subagent)
    {
        yield return Row(host, "PreToolUse", "PreToolUse", "Bash", installer);
        yield return Row(host, "PreToolUse", "PreToolUse", "Read", installer);
        yield return Row(host, "PreToolUse", "PreToolUse", "Skill", installer);
        yield return Row(host, "PostToolUse", "PostToolUse", "*", installer);
        foreach (
            var name in new[]
            {
                "UserPromptSubmit",
                "SessionStart",
                "PreCompact",
                "PostCompact",
                "SessionEnd",
            }
        )
        {
            yield return Row(host, name, name, installer: installer);
        }
        if (subagent)
        {
            yield return Row(host, "SubagentStart", "SubagentStart", installer: installer);
        }
    }
}
