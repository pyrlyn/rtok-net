using System.Diagnostics;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Measure.Tests;

/// <summary><c>rtok bench</c> against a fake agent script: no real agent runs here.</summary>
public sealed class BenchTests : IDisposable
{
    private readonly TempDir _dir = new();

    public BenchTests() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the fake agent is a POSIX shell script");

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Each_config_runs_three_times_with_stdin_closed()
    {
        var log = _dir.Combine("agent.log");
        var agent = Script(
            "agent",
            $$"""
            if read -r line; then echo "stdin: $line" >> '{{log}}'; fi
            echo "run $2 $6" >> '{{log}}'
            case "$6" in
              *a.json) printf '{"usage":{"input_tokens":100,"cache_read_input_tokens":1000,"cache_creation_input_tokens":200,"output_tokens":50},"total_cost_usd":0.01}' ;;
              *b.json) printf '{"usage":{"input_tokens":40,"cache_read_input_tokens":600,"cache_creation_input_tokens":0,"output_tokens":30},"total_cost_usd":0.004}' ;;
            esac
            """
        );
        var config = Config();

        var table = await Bench.RunAsync(config, agent, TestContext.Current.CancellationToken);

        Assert.Equal(
            """
            config        input      cache     output       cost     pass failed
            a               100       1200         50     0.0100    1/2        0
            b                40        600         30     0.0040    1/2        0

            """,
            table
        );
        var runs = File.ReadAllLines(log);
        Assert.Equal(2 * 2 * 3, runs.Length); // tasks × configs × runs, and nothing read from stdin
        Assert.Equal(
            6,
            runs.Count(l =>
                l.StartsWith("run Add a doc comment to main. ", StringComparison.Ordinal)
            )
        );
        Assert.Contains(
            "\"mean_cost_usd\": 0.01",
            File.ReadAllText(_dir.Combine("results", "a.json")),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Dry_run_lists_task_config_and_run()
    {
        var config = Config();
        config.Bench.DryRun = true;
        config.Bench.Runs = 2;

        var plan = await Bench.RunAsync(
            config,
            "/nonexistent/agent",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "add a doc a 1\nadd a doc a 2\nadd a doc b 1\nadd a doc b 2\n"
                + "fail a 1\nfail a 2\nfail b 1\nfail b 2\n",
            plan
        );
    }

    [Fact]
    public async Task A_flood_past_the_cap_a_failure_and_a_hang_are_failed_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var flood = Script("flood", "head -c 9437184 /dev/zero; exit 0");
        var bad = Script("bad", """printf '{"usage":{"input_tokens":5}}'; exit 3""");
        var hang = Script("hang", "exec sleep 30");
        var clock = Stopwatch.StartNew();

        Assert.Null(await Bench.OneAsync(flood, "p", "s", TimeSpan.FromSeconds(30), ct));
        Assert.Null(await Bench.OneAsync(bad, "p", "s", TimeSpan.FromSeconds(30), ct));
        Assert.Null(await Bench.OneAsync(hang, "p", "s", TimeSpan.FromMilliseconds(300), ct));
        Assert.Null(
            await Bench.OneAsync(_dir.Combine("missing"), "p", "s", TimeSpan.FromSeconds(5), ct)
        );
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"{clock.Elapsed}");
    }

    [Fact]
    public async Task A_missing_settings_file_or_task_field_is_refused()
    {
        var config = Config();
        File.Delete(_dir.Combine("b.json"));

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Bench.RunAsync(config, "agent", TestContext.Current.CancellationToken)
        );
        Assert.StartsWith(
            "bench.configs.b: no settings file at ",
            missing.Message,
            StringComparison.Ordinal
        );

        File.WriteAllText(config.Bench.Tasks, "[[tasks]]\nid = \"x\"\nprompt = \"p\"\n");
        var field = Assert.Throws<InvalidOperationException>(() => Bench.Tasks(config.Bench.Tasks));
        Assert.EndsWith("task 1 has no string `check`", field.Message, StringComparison.Ordinal);
    }

    private RtokConfig Config()
    {
        var tasks = _dir.Write(
            "tasks.toml",
            """
            [[tasks]]
            id = "add a doc"
            prompt = "Add a doc comment to main."
            check = "true"

            [[tasks]]
            id = "fail"
            prompt = "Do the impossible."
            check = "exit 1"
            """
        );
        var config = new RtokConfig();
        config.Bench.Tasks = tasks;
        config.Bench.Configs["a"] = _dir.Write("a.json", "{}");
        config.Bench.Configs["b"] = _dir.Write("b.json", "{}");
        return config;
    }

    private string Script(string name, string body)
    {
        var path = _dir.Write(name, "#!/bin/sh\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
        return path;
    }
}
