using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Rtok.Process.Tests;

[Collection(nameof(DisplayState))]
public sealed class ProgressRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Capture_keeps_both_streams_and_the_exit_code()
    {
        var got = await Sh("printf out; printf err >&2; exit 3").RunAsync(Ct);

        Assert.Equal(
            (3, "out", "err", 0L),
            (got.ExitCode, Text(got.Stdout), Text(got.Stderr), got.Dropped)
        );
    }

    [Fact]
    public async Task A_missing_program_fails_to_start()
    {
        await Assert.ThrowsAsync<Win32Exception>(() =>
            ProgressRunner.Quiet("rtok-no-such-program", ["x"]).RunAsync(Ct)
        );
    }

    [Fact]
    public async Task Stdin_is_closed_unless_given_and_cwd_and_env_reach_the_child()
    {
        var closed = await Sh("cat").RunAsync(Ct);
        Assert.Equal((0, ""), (closed.ExitCode, Text(closed.Stdout)));

        var dir = Path.GetFullPath(Path.GetTempPath());
        var got = await Sh("printf '%s|%s' \"$(pwd -P)\" \"$RTOK_T\"")
            .WorkingDirectory(dir)
            .Environment("RTOK_T", "v")
            .RunAsync(Ct);
        Assert.EndsWith("|v", Text(got.Stdout), StringComparison.Ordinal);

        var fed = await Sh("cat").StandardInput(new MemoryStream("in"u8.ToArray())).RunAsync(Ct);
        Assert.Equal("in", Text(fed.Stdout));
    }

    [Fact]
    public async Task A_removed_variable_does_not_reach_the_child()
    {
        var got = await Sh("printf '%s' \"${HOME-unset}\"").Environment("HOME", null).RunAsync(Ct);

        Assert.Equal("unset", Text(got.Stdout));
    }

    [Fact]
    public async Task A_slow_child_is_killed_at_the_timeout()
    {
        var clock = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Sh("exec sleep 5").Timeout(TimeSpan.FromMilliseconds(100)).RunAsync(Ct)
        );

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"{clock.Elapsed}");
        var quick = await Sh("exit 0").Timeout(TimeSpan.FromSeconds(5)).RunAsync(Ct);
        Assert.Equal(0, quick.ExitCode);
    }

    [Fact]
    public async Task An_inherited_slow_child_is_killed_at_the_timeout()
    {
        var clock = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Sh("exec sleep 2")
                .Output(OutputMode.Inherit)
                .Timeout(TimeSpan.FromMilliseconds(100))
                .RunAsync(Ct)
        );

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"{clock.Elapsed}");
    }

    [Fact]
    public async Task An_inherited_child_inside_its_timeout_keeps_its_own_status()
    {
        var done = await Sh("exit 3")
            .Output(OutputMode.Inherit)
            .Timeout(TimeSpan.FromSeconds(5))
            .RunAsync(Ct);

        Assert.Equal((3, 0, 0), (done.ExitCode, done.Stdout.Length, done.Stderr.Length));
    }

    [Fact]
    public async Task An_inherited_child_that_cannot_start_fails_to_start_not_to_time_out()
    {
        await Assert.ThrowsAsync<Win32Exception>(() =>
            ProgressRunner
                .Quiet("/nonexistent/rtok-t", ["x"])
                .Output(OutputMode.Inherit)
                .Timeout(TimeSpan.FromSeconds(5))
                .RunAsync(Ct)
        );
    }

    [Fact]
    public async Task The_parser_sees_both_streams_split_on_carriage_returns()
    {
        var seen = new List<(ChildOutput, string)>();

        var done = await Sh("printf 'a\\rb\\n' >&2; printf 'c\\n'")
            .Progress(
                new CallbackProgress(
                    (stream, line) =>
                    {
                        lock (seen)
                        {
                            seen.Add((stream, line));
                        }
                        return null;
                    }
                )
            )
            .RunAsync(Ct);

        Assert.Equal(("c\n", "a\rb\n"), (Text(done.Stdout), Text(done.Stderr)));
        Assert.Equal(
            [(ChildOutput.Stderr, "a"), (ChildOutput.Stderr, "b"), (ChildOutput.Stdout, "c")],
            seen.OrderBy(s => s.Item2, StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task Stream_and_inherit_leave_nothing_to_capture()
    {
        var streamed = await Sh("exit 4").Output(OutputMode.Stream).RunAsync(Ct);
        Assert.Equal((4, 0), (streamed.ExitCode, streamed.Stdout.Length));

        var inherited = await Sh("exit 0").Output(OutputMode.Inherit).RunAsync(Ct);
        Assert.Equal(0, inherited.ExitCode);
    }

    [Fact]
    public async Task Spawn_and_run_refuse_the_modes_that_do_not_fit()
    {
        using (var detached = Sh("printf x").Output(OutputMode.Detached).Spawn())
        {
            await detached.WaitForExitAsync(Ct);
            Assert.Equal(0, detached.ExitCode);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Sh("exit 0").Output(OutputMode.Detached).RunAsync(Ct)
        );
        Assert.Throws<InvalidOperationException>(() =>
            Sh("exit 0").Output(OutputMode.Stream).Spawn()
        );

        using var piped = Sh("printf x").Spawn();
        var text = await piped.StandardOutput.ReadToEndAsync(Ct);
        await piped.WaitForExitAsync(Ct);
        Assert.Equal("x", text);
    }

    [Fact]
    public async Task A_descendant_holding_the_pipe_does_not_stall_the_run()
    {
        var clock = Stopwatch.StartNew();

        var got = await Sh("echo hi; sleep 20 & exit 4").RunAsync(Ct);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"{clock.Elapsed}");
        Assert.Equal(("hi\n", 4), (Text(got.Stdout), got.ExitCode));
    }

    [Fact]
    public async Task A_cap_keeps_the_first_bytes_and_counts_the_rest()
    {
        var one = await Sh("head -c 5000 /dev/zero | tr '\\0' x; exit 2").RunCappedAsync(1024, Ct);
        Assert.Equal((1024, 3976L, 2), (one.Stdout.Length, one.Dropped, one.ExitCode));

        // The budget is shared by both streams, and the child still exits with its own code.
        var both = await Sh("head -c 50000 /dev/zero; head -c 7000 /dev/zero >&2; exit 3")
            .RunCappedAsync(1000, Ct);
        Assert.Equal(1000, both.Stdout.Length + both.Stderr.Length);
        Assert.Equal((56_000L, 3), (both.Dropped, both.ExitCode));

        var exact = await Sh("printf hello").RunCappedAsync(5, Ct);
        Assert.Equal(("hello", 0L), (Text(exact.Stdout), exact.Dropped));
    }

    [Theory]
    [InlineData("kill -TERM $$", 143)]
    [InlineData("kill -KILL $$", 137)]
    [InlineData("exit 7", 7)]
    public async Task The_exit_code_is_128_plus_the_signal_of_a_killed_child(
        string script,
        int code
    )
    {
        var got = await Sh(script).RunAsync(Ct);

        Assert.Equal(code, got.ExitCode);
    }

    [Fact]
    public async Task The_shim_is_a_no_op_off_windows()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the shim applies on Windows");

        var got = await ProgressRunner
            .Quiet("sh", ["-c", "cat; printf ok"])
            .WindowsShim()
            .RunAsync(Ct);

        Assert.Equal("ok", Text(got.Stdout));
    }

    [Theory]
    [InlineData("npx", true)]
    [InlineData("uvx", true)]
    [InlineData(@"C:\Program Files\nodejs\npx.cmd", true)]
    [InlineData("tool.bat", true)]
    [InlineData("rtok.exe", false)]
    [InlineData(@"C:\Users\u\.ketch\bin\rtok.exe", false)]
    [InlineData("/usr/bin/uvx", false)]
    public void The_cmd_host_wraps_npm_shims_not_exes(string command, bool wrapped) =>
        Assert.Equal(wrapped, ProgressRunner.NeedsCmdHost(command));

    [Fact]
    public async Task A_raw_argument_is_passed_as_written()
    {
        var got = await Sh("printf '%s|' \"$@\"")
            .RawArgument("sh")
            .RawArgument("\"a b\"")
            .RunAsync(Ct);

        Assert.Equal("a b|", Text(got.Stdout));
    }

    private static ProgressRunner Sh(string script)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "needs a POSIX sh");
        return new ProgressRunner("testing…", "sh", ["-c", script]);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
