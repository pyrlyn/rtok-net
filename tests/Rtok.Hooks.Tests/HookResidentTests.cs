using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using Rtok.Hook.Contracts;
using Rtok.Testing;

namespace Rtok.Hooks.Tests;

public sealed class HookResidentTests : IDisposable
{
    private const ulong Fingerprint = 7;
    private readonly TempDir _dir = new("rh-");
    private readonly HookEndpoint _endpoint;
    private readonly ConcurrentQueue<(HookRequest Request, bool Replay)> _calls = new();
    private readonly CancellationTokenSource _cts = new();

    public HookResidentTests() => _endpoint = HookEndpoint.For(_dir.Combine("h"))!;

    private static ResidentOptions Options(string version = "v1") =>
        new()
        {
            Version = version,
            Fingerprint = Fingerprint,
            Tick = TimeSpan.FromMilliseconds(50),
            LockWait = TimeSpan.FromMilliseconds(100),
        };

    private ReadOnlyMemory<byte> Echo(HookRequest request, bool replay)
    {
        _calls.Enqueue((request, replay));
        return Encoding.UTF8.GetBytes($"{{\"event\":\"{request.Event}\"}}");
    }

    // The resident on a thread of its own, once its socket (or pipe) answers.
    private Task<ResidentExit> Start(ResidentOptions? options = null)
    {
        var run = Task.Factory.StartNew(
            () => HookResident.Run(_endpoint, Echo, _cts.Token, options ?? Options()),
            TaskCreationOptions.LongRunning
        );
        var until = Stopwatch.StartNew();
        while (Exchange(Request("probe"), out _) == HookOutcome.Unreachable && !run.IsCompleted)
        {
            Assert.True(until.Elapsed < TimeSpan.FromSeconds(10), "the resident did not come up");
            Thread.Sleep(10);
        }
        while (_calls.TryDequeue(out _)) { }
        return run;
    }

    private static HookRequest Request(
        string hookEvent,
        string version = "v1",
        ulong fingerprint = Fingerprint
    ) => new(version, fingerprint, hookEvent, "", "/w", [], 1, "{}"u8.ToArray());

    private HookOutcome Exchange(HookRequest request, out string stdout, int budgetMs = 2000)
    {
        var outcome = HookClient.Exchange(
            _endpoint,
            HookWire.EncodeRequest(request),
            out var bytes,
            budgetMs
        );
        stdout = Encoding.UTF8.GetString(bytes);
        return outcome;
    }

    [Fact]
    public async Task It_serves_a_request_with_the_handler()
    {
        var run = Start();

        Assert.Equal(HookOutcome.Served, Exchange(Request("PreToolUse"), out var stdout));

        Assert.Equal("""{"event":"PreToolUse"}""", stdout);
        var (request, replay) = Assert.Single(_calls);
        Assert.Equal("/w", request.Cwd);
        Assert.False(replay);
        await _cts.CancelAsync();
        Assert.Equal(ResidentExit.Cancelled, await Exit(run));
        Assert.False(File.Exists(_endpoint.Address));
    }

    [Fact]
    public async Task It_refuses_another_version_and_exits()
    {
        var run = Start();

        Assert.Equal(HookOutcome.RefusedVersion, Exchange(Request("x", version: "v2"), out _));

        Assert.Equal(ResidentExit.VersionRefused, await Exit(run));
        Assert.Empty(_calls);
        Assert.Equal(HookOutcome.Unreachable, Exchange(Request("x"), out _));
    }

    [Fact]
    public void It_refuses_another_fingerprint_and_stays()
    {
        var run = Start();

        Assert.Equal(HookOutcome.RefusedFingerprint, Exchange(Request("x", fingerprint: 8), out _));

        Assert.Equal(HookOutcome.Served, Exchange(Request("y"), out _));
        Assert.False(run.IsCompleted);
        _cts.Cancel();
    }

    [Fact]
    public async Task One_resident_per_home()
    {
        var run = Start();

        var second = HookResident.Run(_endpoint, Echo, CancellationToken.None, Options());

        Assert.Equal(ResidentExit.AlreadyRunning, second);
        Assert.Equal(HookOutcome.Served, Exchange(Request("x"), out _));
        await _cts.CancelAsync();
        await Exit(run);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task The_socket_is_owner_only_inside_a_0700_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix modes");
        }
        var run = Start();

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(_endpoint.RunDir)
        );
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(_endpoint.Address)
        );
        Assert.Empty(Directory.EnumerateFiles(_endpoint.RunDir, ".s*"));
        await _cts.CancelAsync();
        await Exit(run);
    }

    [Fact]
    public async Task It_replays_the_queue_for_its_own_version_only()
    {
        HookBacklog.Enqueue(_endpoint, HookWire.EncodeRequest(Request("SessionEnd")));
        HookBacklog.Enqueue(_endpoint, HookWire.EncodeRequest(Request("Old", version: "v0")));
        HookBacklog.Enqueue(_endpoint, [1, 2, 3]);

        var run = Task.Factory.StartNew(
            () => HookResident.Run(_endpoint, Echo, _cts.Token, Options()),
            TaskCreationOptions.LongRunning
        );

        // The drain deletes each entry as it reads it: wait for the last one, not the first call.
        Assert.True(
            await HookSandbox.UntilAsync(
                () => !Directory.EnumerateFiles(_endpoint.QueueDir).Any(),
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken
            )
        );
        await UntilCalled();
        var (request, replay) = Assert.Single(_calls);
        Assert.Equal("SessionEnd", request.Event);
        Assert.True(replay);
        await _cts.CancelAsync();
        await Exit(run);
    }

    [Fact]
    public async Task A_request_queued_while_it_runs_is_replayed_on_the_next_tick()
    {
        var run = Start();

        HookBacklog.Enqueue(_endpoint, HookWire.EncodeRequest(Request("Late")));

        await UntilCalled();
        Assert.Equal("Late", Assert.Single(_calls).Request.Event);
        await _cts.CancelAsync();
        await Exit(run);
    }

    [Fact]
    public async Task It_exits_when_its_lock_file_vanishes()
    {
        var run = Start();

        File.Delete(_endpoint.LockPath);

        Assert.Equal(ResidentExit.LockLost, await Exit(run));
    }

    [Fact]
    public async Task A_failing_handler_answers_the_no_op_reply()
    {
        var run = Task.Factory.StartNew(
            () =>
                HookResident.Run(
                    _endpoint,
                    (_, _) => throw new InvalidOperationException("boom"),
                    _cts.Token,
                    Options()
                ),
            TaskCreationOptions.LongRunning
        );
        var until = Stopwatch.StartNew();
        string stdout;
        while (Exchange(Request("x"), out stdout) != HookOutcome.Served)
        {
            Assert.True(until.Elapsed < TimeSpan.FromSeconds(10));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("{}", stdout);
        await _cts.CancelAsync();
        await Exit(run);
    }

    [Fact]
    public void No_resident_is_unreachable() =>
        Assert.Equal(HookOutcome.Unreachable, Exchange(Request("x"), out _));

    // A resident that takes the request but never answers costs the client its budget, no more.
    [Fact]
    public void A_hung_resident_fails_within_the_budget()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix socket");
        }
        HookEndpoint.EnsurePrivateDir(_endpoint.RunDir);
        using var listener = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified
        );
        listener.Bind(new UnixDomainSocketEndPoint(_endpoint.Address));
        listener.Listen(8);

        var clock = Stopwatch.StartNew();
        var outcome = Exchange(Request("x"), out _, HookClient.BudgetMs);

        Assert.Equal(HookOutcome.Failed, outcome);
        Assert.InRange(
            clock.ElapsedMilliseconds,
            HookClient.BudgetMs - 5,
            HookClient.BudgetMs + 40
        );
    }

    private static Task<ResidentExit> Exit(Task<ResidentExit> run) =>
        run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private async Task UntilCalled()
    {
        var until = Stopwatch.StartNew();
        while (_calls.IsEmpty && until.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        Thread.Sleep(100);
        _dir.Dispose();
    }
}
