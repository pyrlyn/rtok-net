namespace Rtok.SamplePlugin;

/// <summary>The plugin's own <c>[plugins.shrink]</c> section. A missing section is
/// <c>new ShrinkConfig()</c>, so every property has a usable default.</summary>
public sealed class ShrinkConfig
{
    /// <summary>Estimated tokens the shallow listing is assumed to cost. Configuration lets the
    /// operator tune what the plugin claims, not turn it into something else.</summary>
    public int AssumedAfterTokens { get; init; }
}
