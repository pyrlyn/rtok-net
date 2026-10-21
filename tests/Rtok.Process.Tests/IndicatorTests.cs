namespace Rtok.Process.Tests;

[Collection(nameof(DisplayState))]
public sealed class IndicatorTests : IDisposable
{
    public IndicatorTests() => ProgressDisplay.Reset();

    public void Dispose() => ProgressDisplay.Reset();

    [Theory]
    [InlineData(true, false, null, true)]
    [InlineData(false, false, null, false)]
    [InlineData(true, true, null, false)]
    [InlineData(true, false, "1", false)]
    [InlineData(true, false, "0", true)]
    public void Nothing_is_drawn_without_a_display_or_when_switched_off(
        bool display,
        bool off,
        string? variable,
        bool shown
    ) => Assert.Equal(shown, ProgressDisplay.Shown(display, off, variable));

    [Fact]
    public void A_batch_counts_finished_items_and_names_the_running_one()
    {
        var seen = new Recorder();
        var batch = new Batch(seen, 2, ProgressUnit.Items);
        Assert.Equal((0L, 2L), (batch.Position, batch.Total));

        batch.Step("a");
        Assert.Equal(0, batch.Position);
        batch.Step("b");
        Assert.Equal(1, batch.Position);
        batch.Finish();

        Assert.Equal(["0/2 a", "1/2 b", "2/2 ", "finish"], seen.Events);
    }

    [Fact]
    public void A_batch_never_counts_past_its_total()
    {
        var batch = Batch.Hidden(1, ProgressUnit.Items);
        foreach (var item in (string[])["a", "b", "c"])
        {
            batch.Step(item);
        }
        Assert.Equal(1, batch.Position);
        batch.Finish();
        ProgressRunner.Batch("x…", 3, ProgressUnit.Items).Finish();
    }

    [Fact]
    public void A_wait_clears_its_spinner_after_the_work_and_passes_the_result_through()
    {
        var display = new Display();
        ProgressDisplay.Install(display);

        var got = ProgressRunner.Wait(
            "x…",
            () =>
            {
                Assert.Equal(["spinner x…"], display.Events);
                return 7;
            }
        );

        Assert.Equal(7, got);
        Assert.Equal(["spinner x…", "finish"], display.Events);
        Assert.Throws<InvalidOperationException>(() =>
            ProgressRunner.Wait<int>("y…", () => throw new InvalidOperationException())
        );
        Assert.Equal("finish", display.Events[^1]);
    }

    [Fact]
    public async Task A_run_draws_through_the_installed_display_until_the_process_turns_it_off()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "needs a POSIX sh");
        var display = new Display();
        ProgressDisplay.Install(display);
        var ct = TestContext.Current.CancellationToken;

        await new ProgressRunner(
            "git…",
            "sh",
            ["-c", "printf 'Receiving objects: 50%% (1/2)\\r' >&2"]
        )
            .Progress(new GitProgress())
            .RunAsync(ct);
        await ProgressRunner.Quiet("sh", ["-c", "exit 0"]).RunAsync(ct);
        ProgressDisplay.Disable();
        ProgressRunner.Batch("b…", 1, ProgressUnit.Items).Finish();

        Assert.Equal(["spinner git…", "1/2 Receiving objects", "finish"], display.Events);
    }

    [Fact]
    public void Suspend_runs_the_work_once_with_or_without_a_display()
    {
        var ran = 0;
        ProgressDisplay.Suspend(() => ran++);
        HiddenIndicator.Instance.Suspend(() => ran++);
        ProgressDisplay.Install(new Display());
        ProgressDisplay.Suspend(() => ran++);

        Assert.Equal(3, ran);
    }

    private sealed class Recorder : IIndicator
    {
        public List<string> Events { get; } = [];

        public void Report(ProgressUpdate update) =>
            Events.Add($"{update.Position}/{update.Total} {update.Message}");

        public void Message(string message) => Events.Add($"message {message}");

        public void Suspend(Action write) => write();

        public void Finish() => Events.Add("finish");
    }

    private sealed class Display : IProgressDisplay
    {
        private readonly Recorder recorder = new();

        public List<string> Events => recorder.Events;

        public IIndicator Spinner(string label)
        {
            Events.Add($"spinner {label}");
            return recorder;
        }

        public IIndicator Bar(string label, long total, ProgressUnit unit)
        {
            Events.Add($"bar {label} {total}");
            return recorder;
        }

        public void Suspend(Action write) => write();
    }
}
