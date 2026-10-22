namespace Rtok.Doctor;

/// <summary>One finding of <c>rtok doctor</c>, as <c>--json problems[]</c> prints it.</summary>
/// <param name="Kind">One of <see cref="ProblemKinds"/>.</param>
/// <param name="Agent">The host whose file holds it: <c>claude</c>, <c>codex</c>, …</param>
/// <param name="Source">The file.</param>
/// <param name="Path">Where in the file: <c>hooks.Stop[0].hooks[0]</c>, <c>mcpServers.x</c>.</param>
/// <param name="Detail">What is wrong, in one line.</param>
/// <param name="Fixable">Whether <c>--fix</c> may remove the entry.</param>
public sealed record Problem(
    string Kind,
    string Agent,
    string Source,
    string Path,
    string Detail,
    bool Fixable
)
{
    /// <summary>The hook event; null for an MCP entry or a file.</summary>
    public string? Event { get; init; }

    /// <summary>The hook matcher; null when the group has none.</summary>
    public string? Matcher { get; init; }

    /// <summary>The hook command, or the MCP launch with env values left out.</summary>
    public string? Command { get; init; }

    /// <summary>The duplicate group, numbered from 0; null outside a group.</summary>
    public int? Group { get; init; }

    /// <summary>This copy of a duplicate is the one to keep.</summary>
    public bool Keep { get; init; }

    /// <summary><c>--fix</c> removes it unless deselected: fixable, not kept, not in a shared project file.</summary>
    public bool Selected { get; init; }

    /// <summary>The entry sits in a file <c>--fix</c> may edit, kept or not: what a kept-copy swap needs.</summary>
    internal bool Editable { get; init; }
}
