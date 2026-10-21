using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

namespace Rtok.Smoke;

public sealed class PluginTests
{
    // N12: an out-of-tree plugin is linked at build time, and RtokPlugins=none leaves every
    // built-in one out; the AOT rtok lists only the sample.
    [Fact]
    public async Task Rtok_built_with_the_sample_plugin_lists_only_it()
    {
        var output = await PluginsAsync(PublishedApps.RtokWithSample);
        Assert.Equal(
            "id        kind      enabled   surfaces\nshrink    external  on        hook\n",
            output
        );
    }

    [Fact]
    public async Task A_measure_only_build_publishes_with_aot_and_lists_measure()
    {
        var output = await PluginsAsync(PublishedApps.RtokMeasureOnly);
        Assert.Equal(
            "id        kind      enabled   surfaces\nmeasure   builtin   on        proxy,cli\n",
            output
        );
    }

    private static async Task<string> PluginsAsync(string app)
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync(app, ct);
        var result = await Cli.Wrap(rtok).WithArguments(["plugins"]).ExecuteBufferedAsync(ct);
        return result.StandardOutput;
    }
}
