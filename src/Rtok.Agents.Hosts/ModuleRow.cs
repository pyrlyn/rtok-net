namespace Rtok.Agents.Hosts;

/// <summary>One module on one app, as <c>agents info</c> and its <c>--json</c> report it.</summary>
/// <param name="Name">The module: <c>hooks</c>, <c>mcp</c>, <c>proxy</c>, <c>plugin</c>.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Note">What follows the state word: <c> (--proxy)</c>, <c>: why not</c>, or nothing.</param>
public sealed record ModuleRow(string Name, ModuleState State, string Note)
{
    /// <summary><c>mcp     installed</c>: the name padded to seven, the state word, the note.</summary>
    public string Text() => $"{Name, -7} {State.Word()}{Note}";
}
