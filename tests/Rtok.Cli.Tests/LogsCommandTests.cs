using System.Text;
using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary><c>rtok logs</c>, <c>logs export</c>, <c>logs watch</c> and <c>RTOK_LOG</c>, against the binary.</summary>
public sealed class LogsCommandTests : IDisposable
{
    private readonly TempDir _home = new("rtok-logs-");

    private string LogPath => _home.Combine("logs", "rtok.log");

    [Fact]
    public async Task NothingLoggedSaysSoAndExportIsBare()
    {
        var empty = await RunAsync("logs");
        _home.Write(
            "logs/rtok.log",
            "2026-10-22 10:00:00 info cli/x: a\n2026-10-22 10:00:01 error cli/y: b\n"
        );

        await Verify(
            $"$ rtok logs\n{empty.StandardOutput}$ rtok logs export\n{(await RunAsync("logs", "export")).StandardOutput}"
        );
    }

    [Fact]
    public async Task LogsNumbersNewestFirstAcrossRotation()
    {
        _home.Write("logs/rtok.log", "live-3\nlive-4\n");
        _home.Write("logs/rtok.log.1", "old-1\nold-2\n");

        await Verify((await RunAsync("logs", "--lines", "3")).StandardOutput);
    }

    [Fact]
    public async Task ExportCountsLinesToo()
    {
        _home.Write("logs/rtok.log", "a\nb\n");

        Assert.Equal("b\n", (await RunAsync("logs", "export", "--lines", "1")).StandardOutput);
    }

    [Fact]
    public async Task JsonIsTheLinesNewestFirst()
    {
        Assert.Equal("[]\n", (await RunAsync("logs", "--json")).StandardOutput);
        _home.Write("logs/rtok.log", "a\n\"b\"\n");

        Assert.Equal(
            "[\n  \"\\\"b\\\"\",\n  \"a\"\n]\n",
            (await RunAsync("logs", "export", "--json")).StandardOutput
        );
    }

    [Fact]
    public async Task TheDebugLogIsSilentWithoutTheVariable()
    {
        var result = await RunAsync("logs");

        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task TheDebugLogPrintsArgvWithTheVariable()
    {
        var result = await RunAsync(["logs", "--lines", "1"], ("RTOK_LOG", "debug"));

        Assert.Contains(
            " debug cli/argv: argv [\"logs\",\"--lines\",\"1\"]",
            result.StandardError,
            StringComparison.Ordinal
        );
        Assert.Equal("no logs yet\n", result.StandardOutput);
    }

    [Fact]
    public async Task APipedWatchPrintsEachLineOnceAndKeepsCounting()
    {
        _home.Write("logs/rtok.log", "watch-a\nwatch-b\n");
        var output = new StringBuilder();
        var gate = new Lock();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        var watch = Command(["logs", "watch"])
            .WithStandardOutputPipe(
                PipeTarget.ToDelegate(line =>
                {
                    lock (gate)
                    {
                        output.Append(line).Append('\n');
                    }
                })
            )
            .ExecuteAsync(stop.Token);

        string Text()
        {
            lock (gate)
            {
                return output.ToString();
            }
        }
        async Task WaitForAsync(string text)
        {
            for (var i = 0; i < 200 && !string.Equals(Text(), text, StringComparison.Ordinal); i++)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
            Assert.Equal(text, Text());
        }

        await WaitForAsync("1 watch-b\n2 watch-a\n");
        File.Move(LogPath, LogPath + ".1");
        File.WriteAllText(LogPath, "watch-c\nwatch-d\n");
        await WaitForAsync("1 watch-b\n2 watch-a\n3 watch-c\n4 watch-d\n");
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await watch);

        Assert.DoesNotContain('\u001b', Text());
    }

    public void Dispose() => _home.Dispose();

    private Task<BufferedCommandResult> RunAsync(params string[] args) => RunAsync(args, []);

    private Task<BufferedCommandResult> RunAsync(
        string[] args,
        params (string Name, string Value)[] env
    ) => Command(args, env).ExecuteBufferedAsync(TestContext.Current.CancellationToken).Task;

    // The binary in a home of its own, run from it, so no user config or project file leaks in.
    // CliWrap keeps only the last environment given, so it is built here in one go.
    private Command Command(string[] args, params (string Name, string Value)[] env) =>
        CliWrap
            .Cli.Wrap(RtokBinary.Path)
            .WithArguments(args)
            .WithWorkingDirectory(_home.Path)
            .WithEnvironmentVariables(e =>
            {
                e.Set("RTOK_HOME", _home.Path)
                    .Set("RTOK_CONFIG", null)
                    .Set("RTOK_LOG", null)
                    .Set("NO_COLOR", null);
                foreach (var (name, value) in env)
                {
                    e.Set(name, value);
                }
            })
            .WithValidation(CommandResultValidation.None);
}
