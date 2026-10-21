namespace Rtok.Plugin.Sdk;

/// <summary>
/// One token-reduction method. <see cref="Manifest"/> and <see cref="DashboardPage"/> are
/// required; every event has a no-op default, so a plugin implements only the surfaces its
/// manifest declares.
/// </summary>
/// <remarks>
/// The host calls these on the hot path, where a hook has 10 ms and must exit 0 even when
/// everything went wrong. Keep them cheap and return <see langword="null"/> when in doubt: the
/// host isolates each call and drops only the output of a plugin that throws. Three rules hold
/// for every plugin: fail open; lossless (archive the original before shortening it and put its
/// id in <see cref="Measurement.RefId"/>); a saving that is not a <see cref="Measurement"/>
/// does not exist.
/// </remarks>
public interface IPlugin
{
    /// <summary>Id, surfaces and default state. Read on every dispatch; keep it cheap.</summary>
    Manifest Manifest { get; }

    /// <summary>The page this plugin contributes to <c>rtok web</c> and <c>rtok tui</c>.</summary>
    DashboardPage DashboardPage { get; }

    /// <summary>Before a tool call runs: deny it, rewrite its input, or
    /// <see langword="null"/> for no opinion.</summary>
    PreToolDecision? OnPreTool(PreToolUse ev, Ctx cx) => null;

    /// <summary>After a tool call ran: additional context beside the result. The result itself
    /// cannot be changed.</summary>
    string? OnPostTool(PostToolUse ev, Ctx cx) => null;

    /// <summary>A session starts or resumes: text offered for the context budget.</summary>
    Injection? OnSessionStart(SessionStart ev, Ctx cx) => null;

    /// <summary>A user prompt is about to go: text offered for the same budget.</summary>
    Injection? OnPromptSubmit(PromptSubmit ev, Ctx cx) => null;

    /// <summary>The transcript is about to be compacted: persist state.</summary>
    void OnPreCompact(PreCompact ev, Ctx cx) { }

    /// <summary>A subagent is starting: text offered to it, budgeted the same way.</summary>
    Injection? OnSubagentStart(SubagentStart ev, Ctx cx) => null;

    /// <summary>Tools this plugin adds to <c>rtok mcp</c>.</summary>
    IReadOnlyList<ToolDef> McpTools => [];

    /// <summary>Rewrite the tool results of a provider request passing the proxy; one
    /// <see cref="Measurement"/> per change.</summary>
    IReadOnlyList<Measurement> ProxyFilter(WireRequest request, Ctx cx) => [];
}
