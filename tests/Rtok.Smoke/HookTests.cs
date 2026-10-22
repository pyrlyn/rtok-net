using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;
using CliWrap;
using CliWrap.Buffered;
using Rtok.Hook.Contracts;
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
    // client runs alone (LoneClientAsync), so nothing starts.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Hook_answers_each_fixture_with_the_no_op_reply(string fixture)
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempDir();
        var hook = await LoneClientAsync(home.Path, ct);

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
        using var home = new TempDir();
        var hook = await LoneClientAsync(home.Path, ct);

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
        var served = await RunAsync(box, ct);
        Assert.Empty(Directory.EnumerateFiles(box.Endpoint.QueueDir));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(box.Endpoint.Address)
        );
        var direct = await Cli.Wrap(box.Rtok)
            .WithArguments(["hook", "PreToolUse"])
            .WithEnvironmentVariables(box.Environment)
            .WithStandardInputPipe(PipeSource.FromFile(PreToolUse))
            .ExecuteBufferedAsync(ct);
        Assert.Equal(direct.StandardOutput, served);
    }

    // A second resident on the same home finds the lock taken and exits without binding.
    [Fact]
    public async Task A_second_resident_leaves_the_first_serving()
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
        var bound = new FileInfo(box.Endpoint.Address).CreationTimeUtc;

        var second = await Cli.Wrap(box.Rtok)
            .WithArguments(["hook", "--serve"])
            .WithEnvironmentVariables(box.Environment)
            .ExecuteBufferedAsync(ct)
            .Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Contains("already serves", second.StandardError, StringComparison.Ordinal);
        Assert.Equal(bound, new FileInfo(box.Endpoint.Address).CreationTimeUtc);
        Assert.Empty(Directory.EnumerateFiles(box.Endpoint.RunDir, ".s*"));
        Assert.Equal("{}", await RunAsync(box, ct));
        Assert.Empty(Directory.EnumerateFiles(box.Endpoint.QueueDir));
    }

    // Parallel clients with no resident: the spawn lock lets one of them start it.
    [Fact]
    public async Task Parallel_clients_start_one_resident()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the Windows fallback is measured in T52");
        }
        var ct = TestContext.Current.CancellationToken;
        using var box = await HookSandbox.CreateAsync(ct);

        var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RunAsync(box, ct)));

        // Counted at once: a second resident would still be waiting for the lock.
        var started = Residents(box);
        Assert.All(replies, r => Assert.Equal("{}", r));
        Assert.Equal(1, started);
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
    }

    // The client sends its stdin as it came and prints the resident's reply as it came.
    [Fact]
    public async Task The_client_forwards_stdin_and_the_reply_unchanged()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix socket");
        }
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempDir("rh-");
        var reply = """{"hookSpecificOutput":{"additionalContext":"é \n ✓"}}"""u8.ToArray();
        using var fake = FakeResident.Start(
            home.Path,
            _ => HookWire.EncodeReply(HookReplyStatus.Served, reply)
        );

        var result = await RunAsync(home.Path, PipeSource.FromFile(PreToolUse), ct);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(reply, System.Text.Encoding.UTF8.GetBytes(result.StandardOutput));
        var request = await fake.Request.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(request);
        Assert.Equal("PreToolUse", request.Event);
        Assert.Equal(await File.ReadAllBytesAsync(PreToolUse, ct), request.Stdin.ToArray());
    }

    // A resident that dies with the request read: the client still answers the no-op reply.
    [Fact]
    public async Task A_resident_gone_mid_request_still_gets_the_no_op_reply()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix socket");
        }
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempDir("rh-");
        using var fake = FakeResident.Start(home.Path, _ => null);

        var result = await RunAsync(home.Path, PipeSource.FromFile(PreToolUse), ct);

        Assert.NotNull(await fake.Request.WaitAsync(TimeSpan.FromSeconds(10), ct));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("{}", result.StandardOutput);
    }

    // Stdin over the cap never reaches the resident: the no-op reply and a line on stderr.
    [Fact]
    public async Task Stdin_over_the_cap_bypasses_the_resident()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix socket");
        }
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempDir("rh-");
        using var fake = FakeResident.Start(home.Path, _ => null);

        var result = await RunAsync(
            home.Path,
            PipeSource.FromBytes(new byte[HookWire.MaxInput + 1]),
            ct
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("{}", result.StandardOutput);
        Assert.Contains(
            $"stdin over {HookWire.MaxInput} bytes",
            result.StandardError,
            StringComparison.Ordinal
        );
        Assert.False(fake.Request.IsCompleted);
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
        // No run may spawn a real resident onto the address the test binds.
        using var home = new TempDir("rh-");
        var hook = await LoneClientAsync(home.Path, ct);
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

    private static string PreToolUse => RepoPaths.Fixture("hooks", "pre_tool_use.json");

    // The sandbox's rtok processes, matched by the sandbox directory their binary sits in.
    private static int Residents(HookSandbox box)
    {
        var sandbox = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(box.Rtok)));
        return Process
            .GetProcessesByName("rtok")
            .Count(p =>
            {
                using (p)
                {
                    try
                    {
                        return p.MainModule?.FileName is { } file
                            && string.Equals(
                                Path.GetFileName(
                                    Path.GetDirectoryName(Path.GetDirectoryName(file))
                                ),
                                sandbox,
                                StringComparison.Ordinal
                            );
                    }
                    catch (InvalidOperationException)
                    {
                        return false; // exited meanwhile
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        return false;
                    }
                }
            });
    }

    // The lone client: nothing is started.
    private static async Task<BufferedCommandResult> RunAsync(
        string home,
        PipeSource stdin,
        CancellationToken ct
    ) =>
        await Cli.Wrap(await LoneClientAsync(home, ct))
            .WithArguments("PreToolUse")
            .WithEnvironmentVariables(e => e.Set("RTOK_HOME", home))
            .WithStandardInputPipe(stdin)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

    // The published client copied into the home with no rtok beside it: it starts nothing, even
    // when RTOK_SMOKE_BIN holds rtok next to rtok-hook.
    private static async Task<string> LoneClientAsync(string home, CancellationToken ct)
    {
        var published = await PublishedApps.PathAsync("rtok-hook", ct);
        var dir = Directory.CreateDirectory(Path.Join(home, "client")).FullName;
        var hook = Path.Join(dir, Path.GetFileName(published));
        if (!File.Exists(hook))
        {
            File.Copy(published, hook);
        }
        return hook;
    }

    private static async Task<string> RunAsync(HookSandbox box, CancellationToken ct)
    {
        var result = await Cli.Wrap(box.Hook)
            .WithArguments("PreToolUse")
            .WithEnvironmentVariables(box.Environment)
            .WithStandardInputPipe(PipeSource.FromFile(PreToolUse))
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
