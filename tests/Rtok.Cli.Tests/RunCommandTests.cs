using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary>
/// <c>rtok run</c> and <c>rtok filter</c> end to end. No store host is wired yet (T9), so the
/// output is raw (<c>run</c>) or only compressed (<c>filter</c>).
/// </summary>
public sealed class RunCommandTests
{
    [Fact]
    public void Run_merges_the_streams_in_order_and_exits_with_the_command_code()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "a POSIX shell body");

        var (code, stdout, _) = RtokBinary.Exec(["run", "--", "echo out; echo err >&2; exit 3"]);

        Assert.Equal(3, code);
        Assert.Equal("out\nerr\n", stdout);
    }

    [Fact]
    public void Run_takes_a_command_without_the_double_dash()
    {
        var (code, stdout, _) = RtokBinary.Exec(["run", "echo", "hi"]);

        Assert.Equal(0, code);
        Assert.Equal("hi", stdout.TrimEnd());
    }

    [Fact]
    public void A_signal_death_exits_128_plus_the_signal()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX signals");

        var (code, _, _) = RtokBinary.Exec(["run", "--", "kill -TERM $$"]);

        Assert.Equal(143, code);
    }

    [Fact]
    public void Run_without_a_command_is_a_usage_error()
    {
        var (code, _, stderr) = RtokBinary.Exec(["run"]);

        Assert.Equal(2, code);
        Assert.Contains("missing command", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_compresses_stdin_as_the_hinted_command()
    {
        var input = string.Concat(Enumerable.Repeat("the same line\n", 200));

        var (code, stdout, _) = RtokBinary.Exec(
            ["filter", "--stdin", "--cmd", "cat build.log"],
            stdin: input
        );

        Assert.Equal(0, code);
        Assert.True(stdout.Length < input.Length / 2, stdout);
        Assert.Contains("the same line", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_never_runs_the_hint()
    {
        using var dir = new TempDir();
        var marker = Path.Join(dir.Path, "ran");

        var (code, stdout, _) = RtokBinary.Exec(
            ["filter", "--stdin", "--cmd", $"touch {marker}"],
            stdin: "a\nb\n"
        );

        Assert.Equal(0, code);
        Assert.Equal("a\nb\n", stdout);
        Assert.False(File.Exists(marker));
    }

    // `--archive` takes the `run` path; with no store its bytes go out as they are.
    [Fact]
    public void Filter_archive_without_a_store_prints_stdin()
    {
        var (code, stdout, _) = RtokBinary.Exec(
            ["filter", "--archive", "--no-ref", "--cmd", "ls"],
            stdin: "x"
        );

        Assert.Equal(0, code);
        Assert.Equal("x\n", stdout);
    }
}
