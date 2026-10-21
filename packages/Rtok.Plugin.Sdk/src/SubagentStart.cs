namespace Rtok.Plugin.Sdk;

/// <summary>
/// A subagent about to start, before it processes anything (rtok T130). This is where a spawn
/// brief goes: hosts ignore a rewritten input for their agent tools, but deliver the additional
/// context of this event to the subagent.
/// </summary>
/// <param name="AgentType">The agent type being spawned, e.g. <c>general-purpose</c>.</param>
/// <param name="TaskDescription">The host's short description of the task; empty when it
/// sends none.</param>
public sealed record SubagentStart(string AgentType, string TaskDescription);
