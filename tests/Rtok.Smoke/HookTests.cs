using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;
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

    // Fail open: every payload, and no payload at all, gets the no-op reply and exit 0. The
    // published client has no rtok beside it, so nothing starts.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Hook_answers_each_fixture_with_the_no_op_reply(string fixture)
    {
        var ct = TestContext.Current.CancellationToken;
        var hook = await PublishedApps.PathAsync("rtok-hook", ct);
        using var home = new TempDir();

        var result = await Cli.Wrap(hook)
            .WithEnvironmentVariables(e => e.Set("RTOK_HOME", home.Path))
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
        using var home = new TempDir();

        var result = await Cli.Wrap(hook)
            .WithEnvironmentVariables(e => e.Set("RTOK_HOME", home.Path))
            .ExecuteBufferedAsync(ct);

        Assert.Equal("{}", result.StandardOutput);
    }

    // The first call finds no resident: it answers the no-op reply, queues its request and
    // starts the resident, which replays it. The next call is served: nothing more is queued.
    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task The_client_starts_the_resident_which_then_serves_it()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the Windows fallback is measured in T52");
        }
        var ct = TestContext.Current.CancellationToken;
        using var box = await HookSandbox.CreateAsync(ct);

        Assert.Equal("{}", await RunAsync(box, ct));

        Assert.True(
            await HookSandbox.UntilAsync(
                () => File.Exists(box.Endpoint.Address),
                TimeSpan.FromSeconds(10),
                ct
            )
        );
        Assert.True(
            await HookSandbox.UntilAsync(
                () => !Directory.EnumerateFiles(box.Endpoint.QueueDir).Any(),
                TimeSpan.FromSeconds(10),
                ct
            )
        );
        Assert.Equal("{}", await RunAsync(box, ct));
        Assert.Empty(Directory.EnumerateFiles(box.Endpoint.QueueDir));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(box.Endpoint.Address)
        );
    }

    // A client whose config differs from the resident's is refused; it still answers at once and
    // hands its request to a one-shot rtok hook.
    [Fact]
    public async Task A_client_with_another_fingerprint_still_answers()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the Windows fallback is measured in T52");
        }
        var ct = TestContext.Current.CancellationToken;
        using var box = await HookSandbox.CreateAsync(ct);
        await RunAsync(box, ct);
        Assert.True(
            await HookSandbox.UntilAsync(
                () => File.Exists(box.Endpoint.Address),
                TimeSpan.FromSeconds(10),
                ct
            )
        );

        var result = await Cli.Wrap(box.Hook)
            .WithArguments("PreToolUse")
            .WithEnvironmentVariables(e =>
                e.Set("RTOK_HOME", box.Home).Set("RTOK_SMOKE_OTHER", "1")
            )
            .WithStandardInputPipe(
                PipeSource.FromFile(RepoPaths.Fixture("hooks", "pre_tool_use.json"))
            )
            .ExecuteBufferedAsync(ct);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("{}", result.StandardOutput);
        Assert.Empty(Directory.EnumerateFiles(box.Endpoint.QueueDir));
    }

    // With a resident that takes the request and never answers, the client answers after its
    // 50 ms budget plus its own start (catalog B1).
    [Fact]
    public async Task A_hung_resident_costs_the_client_its_budget_and_no_more()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix socket");
        }
        var ct = TestContext.Current.CancellationToken;
        var hook = await PublishedApps.PathAsync("rtok-hook", ct);
        using var home = new TempDir("rh-");
        var start = await FastestAsync(hook, home.Path, ct);
        var endpoint = Hook.Contracts.HookEndpoint.For(home.Path)!;
        Hook.Contracts.HookEndpoint.EnsurePrivateDir(endpoint.RunDir);
        using var listener = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified
        );
        listener.Bind(new UnixDomainSocketEndPoint(endpoint.Address));
        listener.Listen(16);

        var hung = await FastestAsync(hook, home.Path, ct);

        Assert.InRange(
            hung,
            Hook.Contracts.HookClient.BudgetMs,
            start + Hook.Contracts.HookClient.BudgetMs + 50
        );
    }

    private static async Task<string> RunAsync(HookSandbox box, CancellationToken ct)
    {
        var result = await Cli.Wrap(box.Hook)
            .WithArguments("PreToolUse")
            .WithEnvironmentVariables(box.Environment)
            .WithStandardInputPipe(
                PipeSource.FromFile(RepoPaths.Fixture("hooks", "pre_tool_use.json"))
            )
            .ExecuteBufferedAsync(ct);
        Assert.Equal(0, result.ExitCode);
        return result.StandardOutput;
    }

    // The fastest of five runs in milliseconds: the least disturbed by the machine.
    private static async Task<double> FastestAsync(string hook, string home, CancellationToken ct)
    {
        var best = double.MaxValue;
        for (var i = 0; i < 5; i++)
        {
            var clock = Stopwatch.StartNew();
            var result = await Cli.Wrap(hook)
                .WithEnvironmentVariables(e => e.Set("RTOK_HOME", home))
                .WithStandardInputPipe(PipeSource.FromString("{}"))
                .ExecuteBufferedAsync(ct);
            Assert.Equal("{}", result.StandardOutput);
            best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
        }
        return best;
    }
}
