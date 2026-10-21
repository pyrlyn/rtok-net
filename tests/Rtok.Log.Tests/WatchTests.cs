using Rtok.Testing;

namespace Rtok.Log.Tests;

/// <summary><c>rtok logs watch</c>: repaint on a terminal, append when piped, stop cleanly.</summary>
public sealed class WatchTests : IDisposable
{
    private readonly TempDir _dir = new("rtok-watch-");

    private string LogPath => _dir.Combine("rtok.log");

    [Fact]
    public void PipedOnlyTheFreshRowsAreAppended()
    {
        var output = new StringWriter();
        var ticks = new Queue<WatchTick>([
            new(["1 a"], ["1 a"]),
            new([], ["1 a"]),
            new(["2 b"], ["1 b", "2 a"]),
        ]);

        LogWatch.Loop(output, tty: false, TimeSpan.Zero, Next(ticks), CancellationToken.None);

        Assert.Equal("1 a\n2 b\n", output.ToString());
    }

    [Fact]
    public void OnATerminalTheScreenIsRedrawnInPlace()
    {
        var output = new StringWriter();
        var ticks = new Queue<WatchTick>([
            new(["1 a"], ["1 a"]),
            new([], ["1 a"]),
            new(["2 b"], ["1 b", "2 a"]),
        ]);

        LogWatch.Loop(output, tty: true, TimeSpan.Zero, Next(ticks), CancellationToken.None);

        Assert.Equal("\u001b[J1 a\n\u001b[1F\u001b[J1 b\n2 a\n", output.ToString());
    }

    [Fact]
    public void AReaderThatWentAwayEndsTheWatch()
    {
        var steps = 0;

        LogWatch.Loop(
            new BrokenPipe(),
            tty: false,
            TimeSpan.Zero,
            () => ++steps < 100 ? new WatchTick(["row"], ["row"]) : null,
            CancellationToken.None
        );

        Assert.Equal(1, steps);
    }

    [Fact]
    public async Task APipedWatchKeepsCountingAcrossARotation()
    {
        File.WriteAllText(LogPath, "a\nb\n");
        var output = new LockedWriter();
        using var stop = new CancellationTokenSource();
        var watch = Task.Run(
            () => LogWatch.Run(LogPath, 10, output, tty: false, colour: false, stop.Token),
            TestContext.Current.CancellationToken
        );

        await output.WaitForAsync("1 b\n2 a\n");
        File.AppendAllText(LogPath, "c\n");
        await output.WaitForAsync("1 b\n2 a\n3 c\n");
        File.Move(LogPath, LogPath + ".1");
        File.WriteAllText(LogPath, "d\n");
        await output.WaitForAsync("1 b\n2 a\n3 c\n4 d\n");
        await stop.CancelAsync();
        await watch;

        Assert.Equal("1 b\n2 a\n3 c\n4 d\n", output.Text);
    }

    public void Dispose() => _dir.Dispose();

    private static Func<WatchTick?> Next(Queue<WatchTick> ticks) =>
        () => ticks.TryDequeue(out var tick) ? tick : null;

    private sealed class BrokenPipe : StringWriter
    {
        public override void Write(char value) => throw new IOException("Broken pipe");

        public override void Write(string? value) => throw new IOException("Broken pipe");
    }

    /// <summary>A writer the watch thread fills while the test reads it.</summary>
    private sealed class LockedWriter : StringWriter
    {
        private readonly Lock _gate = new();

        public string Text
        {
            get
            {
                lock (_gate)
                {
                    return ToString();
                }
            }
        }

        public override void Write(char value)
        {
            lock (_gate)
            {
                base.Write(value);
            }
        }

        public override void Write(string? value)
        {
            lock (_gate)
            {
                base.Write(value);
            }
        }

        public async Task WaitForAsync(string text)
        {
            for (var i = 0; i < 100 && !string.Equals(Text, text, StringComparison.Ordinal); i++)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
            Assert.Equal(text, Text);
        }
    }
}
