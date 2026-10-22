using System.Text.Json.Nodes;

namespace Rtok.Hooks.Hosts;

/// <summary>What the dispatch answers a hook, before a host writer shapes it (Claude's
/// <c>hookSpecificOutput</c>). An empty reply is the no-op <c>{}</c>.</summary>
/// <param name="EventName">The canonical event the reply answers.</param>
public sealed record HookReply(string EventName)
{
    /// <summary><c>deny</c> to block the call; a rewrite carries none.</summary>
    public string? Decision { get; init; }

    /// <summary>Why the call was denied or rewritten.</summary>
    public string? Reason { get; init; }

    /// <summary>The tool input to run instead.</summary>
    public JsonNode? UpdatedInput { get; init; }

    /// <summary>Text added to the model's context.</summary>
    public string? AdditionalContext { get; init; }

    /// <summary>True when the reply says nothing.</summary>
    public bool IsEmpty =>
        Decision is null && Reason is null && UpdatedInput is null && AdditionalContext is null;

    public static HookReply Deny(string eventName, string reason) =>
        new(eventName) { Decision = "deny", Reason = reason };
}
