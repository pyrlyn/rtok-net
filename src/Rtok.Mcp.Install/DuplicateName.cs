namespace Rtok.Mcp.Install;

/// <summary>
/// What a host does when a plugin serves a server under the same name as a config entry
/// (rtok D33, research §25). Only <see cref="ShowsBoth"/> and <see cref="OverridesByScope"/>
/// are confirmed on a host; the other two wait for a host whose docs describe them.
/// </summary>
public enum DuplicateName
{
    /// <summary>Both are listed, the plugin's under its own namespace (Claude Code).</summary>
    ShowsBoth,

    /// <summary>The narrower scope wins (Gemini's settings layers).</summary>
    OverridesByScope,

    /// <summary>The two definitions are merged.</summary>
    Merges,

    /// <summary>The host refuses to start the duplicate.</summary>
    Errors,
}
