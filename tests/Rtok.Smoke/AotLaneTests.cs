using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

namespace Rtok.Smoke;

public sealed class AotLaneTests
{
    // tests/Rtok.AotTests runs only as its published NativeAOT executable (an MTP test host).
    [Fact]
    public async Task Aot_tests_pass_as_a_native_executable()
    {
        var ct = TestContext.Current.CancellationToken;
        var aotTests = await PublishedApps.PathAsync("Rtok.AotTests", ct);

        var result = await Cli.Wrap(aotTests)
            .WithArguments(["--results-directory", Path.Join(Path.GetTempPath(), "rtok-aot-tests")])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
    }
}
