namespace Rtok.Doctor;

/// <summary>One file hooks are read from.</summary>
/// <param name="Agent">The host that runs them.</param>
/// <param name="Path">The file.</param>
/// <param name="Scope">Where it comes from.</param>
/// <param name="Shape">How it spells hooks.</param>
/// <param name="PluginRoot">The plugin's directory, which <c>${CLAUDE_PLUGIN_ROOT}</c> names inside it.</param>
internal sealed record HookSource(
    string Agent,
    string Path,
    Scope Scope,
    HookShape Shape,
    string? PluginRoot = null
)
{
    /// <summary><c>--fix</c> may edit it: the user's or the project's own JSON, never a plugin's, a managed or a TOML file.</summary>
    public bool Editable =>
        Shape == HookShape.Json && Scope is Scope.Project or Scope.User or Scope.Local;

    /// <summary>A relative script resolves against the project: documented for Claude Code only.</summary>
    public bool RelativeOk => string.Equals(Agent, "claude", StringComparison.Ordinal);

    /// <summary>A TOML file.</summary>
    public bool Toml => Shape != HookShape.Json;
}
