using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Measure;

/// <summary>
/// The measure plugin: it shortens nothing, so it saves nothing itself. It owns the stats that
/// come from transcripts and proxy usage; only Measurement rows count as savings.
/// </summary>
public sealed class MeasurePlugin : IPlugin
{
    /// <inheritdoc/>
    public Manifest Manifest { get; } =
        new("measure", Surface.Cli | Surface.Proxy, DefaultOn: true);

    /// <inheritdoc/>
    public DashboardPage DashboardPage { get; } =
        new(
            "Measure",
            "Only Measurement rows count as savings. Stats from transcripts and proxy usage.",
            SavesTokens: false
        );
}
