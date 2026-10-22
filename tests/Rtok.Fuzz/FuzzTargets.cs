using Rtok.Fuzz.Targets;

namespace Rtok.Fuzz;

/// <summary>
/// Every fuzz target, by name. A target is wired by the task that lands the code it feeds and
/// named in that task's <c>Fuzz:</c> line (plan/README.md N11); until then it has no body.
/// </summary>
internal static class FuzzTargets
{
    public static readonly FuzzTarget[] All =
    [
        new("config-toml", "T53", ConfigToml.Run),
        new("durations", "T53", Durations.Run),
        new("help-page", "T53", HelpPageText.Run),
        new("host-json", "T53", HostJson.Run),
        new("ignore", "T53", IgnoreRules.Run),
        new("jsonc-edit", "T53", JsoncEdit.Run),
        new("mcp-config", "T53", McpConfigText.Run),
        new("shell-words", "T53", ShellWordsText.Run),
        new("toml-edit", "T53", TomlEdit.Run),
        new("expand-range", "T9", ExpandRange.Run),
        new("log-tail", "T10", LogTailText.Run),
        new("cli-argv", "T11", CliArgv.Run),
        new("change-preview", "T12.2", ChangePreviewText.Run),
        new("trash-info", "T12.7", TrashInfo.Run),
        new("hook-io", "T13", HookIo.Run),
        new("hook-frame", "T14", HookFrame.Run),
        new("mcp-jsonrpc", "T15", null),
        new("bash-rewrite", "T17", BashRewriteText.Run),
        new("cmd-filter", "T17", CmdFilter.Run),
        new("cmd-formatters", "T18", CmdFormatters.Run),
        new("read-roots", "T19", null),
        new("text-ops", "T19", null),
        new("guard-key", "T20", null),
        new("prose-compress", "T21", ProseCompress.Run),
        new("mem-text", "T22", null),
        new("extractor-reply", "T22.1", null),
        new("checkpoint", "T23", null),
        new("outline", "T24", Outline.Run),
        new("lsp-frame", "T24.1", null),
        new("manifest-refs", "T25", null),
        new("proxy-wire", "T27", null),
        new("proxy-rewrite", "T28", null),
        new("lane-classify", "T29", null),
        new("transcript-jsonl", "T30", TranscriptJsonl.Run),
        new("report-html", "T31", null),
        new("doctor-config", "T32", DoctorConfig.Run),
        new("agents-install", "T35", AgentsInstall.Run),
        new("agent-message", "T37", null),
        new("usage-logs", "T38", UsageLogs.Run),
        new("worktree-arg", "T40", null),
        new("task-file", "T41", null),
        new("lockfiles", "T42", Targets.Lockfiles.Run),
        new("ws-message", "T44", null),
        new("demon-state", "T46", null),
        new("otlp-encode", "T47", null),
        new("win-quote", "T48", null),
    ];
}
