namespace Rtok.Doctor;

/// <summary>One entry <c>--fix</c> may remove.</summary>
/// <param name="Source">The file.</param>
/// <param name="Path">The key path.</param>
/// <param name="Kind">The problem kind.</param>
/// <param name="Agent">The host.</param>
/// <param name="Label"><c>Stop</c>, <c>PreToolUse[Bash]</c> or <c>mcp mcpServers.x</c>.</param>
/// <param name="Command">The hook command or MCP launch.</param>
/// <param name="Detail">Why it goes.</param>
/// <param name="KeptIn">For a duplicate, the file of the copy that stays.</param>
/// <param name="Shared">It sits in a file the project shares, so it starts unselected.</param>
/// <param name="Selected">It is removed when the plan is applied.</param>
/// <param name="CanKeep">It is a copy of a duplicate the user may keep instead.</param>
public sealed record FixItem(
    string Source,
    string Path,
    string Kind,
    string Agent,
    string Label,
    string Command,
    string Detail,
    string? KeptIn,
    bool Shared,
    bool Selected,
    bool CanKeep
);
