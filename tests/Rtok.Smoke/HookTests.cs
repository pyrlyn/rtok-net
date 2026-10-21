using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

namespace Rtok.Smoke;

public sealed class HookTests
{
    public static TheoryData<string> Fixtures() =>
        new(
            Directory
                .EnumerateFiles(RepoPaths.Fixture("hooks"), "*.json")
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)!
        );

    // Fail open: every payload, and no payload at all, gets the no-op reply and exit 0.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Hook_answers_each_fixture_with_the_no_op_reply(string fixture)
    {
        var ct = TestContext.Current.CancellationToken;
        var hook = await PublishedApps.PathAsync("rtok-hook", ct);

        var result = await Cli.Wrap(hook)
            .WithStandardInputPipe(PipeSource.FromFile(RepoPaths.Fixture("hooks", fixture)))
            .ExecuteBufferedAsync(ct);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("{}", result.StandardOutput);
    }

    [Fact]
    public async Task Hook_answers_empty_stdin_with_the_no_op_reply()
    {
        var ct = TestContext.Current.CancellationToken;
        var hook = await PublishedApps.PathAsync("rtok-hook", ct);

        var result = await Cli.Wrap(hook).ExecuteBufferedAsync(ct);

        Assert.Equal("{}", result.StandardOutput);
    }
}
