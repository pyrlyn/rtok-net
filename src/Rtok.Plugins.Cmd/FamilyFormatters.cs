namespace Rtok.Plugins.Cmd;

/// <summary>
/// The family formatter arms (git, cargo, grep, pytest, docker, kubectl, …) and the pre-passes
/// that clean a wrapper's or loop's output before routing. They are ported in T18; until then
/// every family takes its TOML rule, which is rtok's own fallback when a formatter declines.
/// </summary>
internal static class FamilyFormatters
{
    /// <summary>The family formatter's text for <paramref name="output"/>, or null to fall back
    /// to the rule.</summary>
    public static string? Format(IReadOnlyList<string> argv, string output) => null;

    /// <summary>Output with a wrapper's chatter (mise) or a polling loop's repeats removed, or
    /// null when <paramref name="stem"/> has no pre-pass or it changed nothing.</summary>
    public static string? Tidy(string stem, string output) => null;
}
