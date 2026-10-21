namespace Rtok.Config;

/// <summary>the rtok agent registry</summary>
/// <remarks>Read from [agents].</remarks>
public sealed class AgentsSection
{
    /// <summary>false = hooks skip agent register/touch/end (session bookkeeping is unaffected)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>`live()`'s window: no `ended_at` and `last_seen` within this of now</summary>
    public string Idle { get; set; } = "30m";

    /// <summary>bytes of framed messages one `UserPromptSubmit`/`PostToolUse` pushes into the agent's context</summary>
    public uint PushBytes { get; set; } = 1024;

    /// <summary>The [agents.usage] table.</summary>
    public AgentsUsageSection Usage { get; set; } = new();

    /// <summary>The [agents.junk] table.</summary>
    public AgentsJunkSection Junk { get; set; } = new();
}
