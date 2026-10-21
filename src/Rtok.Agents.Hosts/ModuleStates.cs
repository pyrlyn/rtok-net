namespace Rtok.Agents.Hosts;

/// <summary>How a <see cref="ModuleState"/> prints.</summary>
public static class ModuleStates
{
    /// <summary><c>installed</c>, <c>not installed</c>, <c>not supported</c>.</summary>
    public static string Word(this ModuleState state) =>
        state switch
        {
            ModuleState.Installed => "installed",
            ModuleState.NotInstalled => "not installed",
            _ => "not supported",
        };

    /// <summary>The mark before a module line: <c>✓ </c>, <c>✗ </c>, <c>− </c>.</summary>
    public static string Mark(this ModuleState state) =>
        state switch
        {
            ModuleState.Installed => "✓ ",
            ModuleState.NotInstalled => "✗ ",
            _ => "− ",
        };
}
