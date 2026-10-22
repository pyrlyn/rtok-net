namespace Rtok.Measure;

/// <summary>
/// One request of an agent session as its host's log recorded it, in one shape for every host.
/// A sub-agent's requests carry their parent's <see cref="Session"/> and the sub-agent's type in
/// <see cref="Agent"/>.
/// </summary>
/// <param name="Host">The host id: <c>claude</c>, <c>codex</c>, <c>opencode</c>, <c>cursor</c>.</param>
/// <param name="Session">The session the request is billed to.</param>
/// <param name="Agent">The sub-agent type, or null for the session's own requests.</param>
/// <param name="Model">The raw model id, when the log names it.</param>
/// <param name="Ts">Unix seconds, 0 when the log has no time.</param>
/// <param name="Input">Uncached input tokens.</param>
/// <param name="CacheCreate">Tokens written to the prompt cache.</param>
/// <param name="CacheRead">Tokens read from the prompt cache.</param>
/// <param name="Output">Output tokens, reasoning included.</param>
/// <param name="Tools">The tools the request called, in order.</param>
public sealed record CallRecord(
    string Host,
    string Session,
    string? Agent,
    string? Model,
    long Ts,
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output,
    IReadOnlyList<string> Tools
);
