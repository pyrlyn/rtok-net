using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

namespace Rtok.Smoke;

public sealed class CliTests
{
    [Fact]
    public async Task Version_prints_name_version_and_commit()
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync("rtok", ct);

        var result = await Cli.Wrap(rtok).WithArguments("--version").ExecuteBufferedAsync(ct);

        Assert.Matches(
            @"^rtok \d+\.\d+\.\d+ \(([0-9a-f]{9}|unknown)\)\r?\n$",
            result.StandardOutput
        );
    }
}
