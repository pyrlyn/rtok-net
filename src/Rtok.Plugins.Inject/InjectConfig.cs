namespace Rtok.Plugins.Inject;

/// <summary>The plugin's own <c>[plugins.inject]</c> section, as far as the plugin reads it.
/// The budget is core's (<see cref="Core.InjectBudget.Tokens"/>).</summary>
public sealed class InjectConfig
{
    /// <summary>Mode names to inject; when empty, <c>[setup] modes</c> applies.</summary>
    public IList<string> Modes { get; init; } = [];

    /// <summary>Where mode files live. A file there wins over the built-in mode of the same
    /// name; without it only built-in modes inject.</summary>
    public string? ModesDir { get; init; }
}
