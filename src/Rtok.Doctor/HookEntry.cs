namespace Rtok.Doctor;

/// <summary>One command hook.</summary>
/// <param name="Source">The file.</param>
/// <param name="Path">Its key path: <c>hooks.Stop[0].hooks[1]</c>, <c>hooks[2]</c>.</param>
/// <param name="Event">The event.</param>
/// <param name="Matcher">The group's matcher; null for none.</param>
/// <param name="Command">The command line.</param>
internal sealed record HookEntry(
    HookSource Source,
    string Path,
    string Event,
    string? Matcher,
    string Command
)
{
    public string Agent => Source.Agent;
}
