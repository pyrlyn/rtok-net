namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>mise.rs</c> tests: install steps and progress frames.</summary>
public sealed class MiseTests
{
    private const string Install =
        "mise node@20.11.0 download node-v20.11.0-darwin-arm64.tar.gz\n"
        + "mise node@20.11.0 checksum node-v20.11.0-darwin-arm64.tar.gz\n"
        + "mise node@20.11.0 extract node-v20.11.0-darwin-arm64.tar.gz\n"
        + "mise node@20.11.0 node-v20.11.0-darwin-arm64/bin/node -v\n"
        + "mise node@20.11.0 ✓ installed\n"
        + "[test] $ cargo test\n"
        + "test download::ok ... ok\n"
        + "test result: ok. 1 passed\n";

    [Fact]
    public void Install_steps_go_and_everything_else_stays()
    {
        var got = Mise.Tidy(Install);
        Assert.NotNull(got);
        Assert.StartsWith(
            "mise: 3 install/progress lines dropped\n",
            got,
            StringComparison.Ordinal
        );
        foreach (
            var kept in new[]
            {
                "mise node@20.11.0 ✓ installed",
                "mise node@20.11.0 node-v20.11.0-darwin-arm64/bin/node -v",
                "[test] $ cargo test",
                "test download::ok ... ok",
                "test result: ok. 1 passed",
            }
        )
        {
            Assert.Contains(kept, got, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Progress_frames_collapse_and_carriage_returns_show_the_last_frame()
    {
        var got = Mise.Tidy(
            "mise node@20 download x.tar.gz [=>        ] 1.0MiB/45MiB\r"
                + "mise node@20 download x.tar.gz [=====>    ] 20MiB/45MiB\n"
                + "\u001b[32m  [#########>] 45MiB/45MiB\u001b[0m\n"
                + "done\n"
        );
        Assert.NotNull(got);
        Assert.EndsWith("\ndone", got, StringComparison.Ordinal);
        Assert.StartsWith("mise: 2 install/progress lines dropped", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Warnings_and_errors_are_never_dropped_even_on_a_step_line()
    {
        var got = Mise.Tidy(
            "mise node@20 download x.tar.gz\n"
                + "mise node@20 checksum x.tar.gz\n"
                + "mise node@20 extract x.tar.gz\n"
                + "mise node@20 download y.tar.gz\n"
                + "mise node@20 download failed: connection reset\n"
                + "mise WARN  deprecated setting\n"
                + "mise ERROR task failed\n"
        );
        Assert.NotNull(got);
        Assert.Contains("download failed: connection reset", got, StringComparison.Ordinal);
        Assert.Contains("mise WARN  deprecated setting", got, StringComparison.Ordinal);
        Assert.Contains("mise ERROR task failed", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_without_noise_is_left_alone()
    {
        Assert.Null(Mise.Tidy("[test] $ cargo test\ntest result: ok\n"));
        Assert.Null(Mise.Tidy(""));
    }
}
