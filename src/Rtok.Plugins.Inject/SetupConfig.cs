namespace Rtok.Plugins.Inject;

/// <summary>The <c>[setup]</c> keys the plugin reads: <c>agents install --mode</c> writes the
/// modes there.</summary>
public sealed class SetupConfig
{
    /// <summary>Mode names chosen at install time.</summary>
    public IList<string> Modes { get; init; } = [];
}
