using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary><c>rtok bench</c> flags through the config loader; the runs themselves are tested in Rtok.Measure.Tests.</summary>
public sealed class BenchCommandTests : IDisposable
{
    private readonly TempDir _home = new("rtok-bench-");

    public void Dispose() => _home.Dispose();

    [Fact]
    public void Dry_run_lists_the_flags_plan()
    {
        var tasks = _home.Write(
            "tasks.toml",
            "[[tasks]]\nid = \"t1\"\nprompt = \"p\"\ncheck = \"true\"\n"
        );

        var run = Bench("--dry-run", "--tasks", tasks, "--runs", "2");

        Assert.Equal((0, "t1 a 1\nt1 a 2\nt1 b 1\nt1 b 2\n", ""), run);
    }

    [Fact]
    public void An_unknown_suite_exits_1()
    {
        var run = Bench("--suite", "graph");

        Assert.Equal((1, ""), (run.Code, run.Stdout));
        Assert.Equal("Error: bench.suite: unknown suite \"graph\"\n", run.Stderr);
    }

    private (int Code, string Stdout, string Stderr) Bench(params string[] flags) =>
        RtokBinary.Exec(
            ["bench", .. flags],
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["HOME"] = _home.Path,
                ["RTOK_HOME"] = _home.Path,
            }
        );
}
