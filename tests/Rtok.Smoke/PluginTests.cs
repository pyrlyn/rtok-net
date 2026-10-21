using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

namespace Rtok.Smoke;

public sealed class PluginTests
{
    // N12: an out-of-tree plugin is linked at build time; the AOT rtok lists it.
    [Fact]
    public async Task Rtok_built_with_the_sample_plugin_lists_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync(PublishedApps.RtokWithSample, ct);
        var result = await Cli.Wrap(rtok).WithArguments(["plugins"]).ExecuteBufferedAsync(ct);
        Assert.Equal(
            "id        enabled   surfaces\nshrink    on        hook\n",
            result.StandardOutput
        );
    }
}
