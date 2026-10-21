using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary><c>rtok agents usage</c> end to end: flags through the config loader, the store, stdout and stderr.</summary>
public sealed class AgentsUsageTests : IDisposable
{
    private readonly TempDir _home = new("rtok-usage-");

    public void Dispose() => _home.Dispose();

    [Fact]
    public void The_logs_golden_with_its_skipped_host()
    {
        Assert.SkipWhen(
            !TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Kyiv", out _),
            "no IANA zones here"
        );

        var run = Usage("--tz", "Europe/Kyiv");

        Assert.Equal(0, run.Code);
        Assert.StartsWith(
            "rtok agents usage: logs from 6 agents, up to 2026-10-02 (Europe/Kyiv)\n\n  6.85M tokens\n  $7.70 estimated cost\n",
            run.Stdout,
            StringComparison.Ordinal
        );
        Assert.Equal(
            $"skipped grok: unsupported: xAI documents `grok usage`, not the session files in {Fixture("grok")}\n",
            run.Stderr
        );
    }

    [Theory]
    [InlineData("screen")]
    [InlineData("daily", "--daily")]
    [InlineData("json", "--json")]
    public Task The_fixtures_render_like_rtok(string name, params string[] flags)
    {
        var run = Usage(["--tz", "UTC", .. flags]);

        Assert.Equal(0, run.Code);
        return Verify(run.Stdout.Replace(Fixture(""), "{fixtures}", StringComparison.Ordinal))
            .UseParameters(name);
    }

    [Fact]
    public void Hosts_daily_and_unpriced_flags_reach_the_report()
    {
        var run = Usage("--tz", "UTC", "--host", "claude, codex", "--daily", "--unpriced");

        Assert.Equal(0, run.Code);
        Assert.Equal("Model         Agent  Tokens\nmystery-model claude   550K\n", run.Stdout);
        Assert.Equal("", run.Stderr);
    }

    [Fact]
    public void The_store_is_read_for_source_rtok()
    {
        var run = Usage("--source", "rtok", "--tz", "UTC", "--json");

        Assert.Equal(0, run.Code);
        Assert.Contains(
            "\"source\": \"rtok\",\n  \"tz\": \"UTC\",\n  \"through\": null,",
            run.Stdout,
            StringComparison.Ordinal
        );
        Assert.True(File.Exists(_home.Combine("store", "rtok.db")));
    }

    [Theory]
    [InlineData("--by", "host", "agents.usage.by `host`: expected `agent` or `model`")]
    [InlineData(
        "--source",
        "proxy",
        "agents.usage.source `proxy`: expected `logs`, `rtok` or `both`"
    )]
    [InlineData(
        "--until",
        "yesterday",
        "agents.usage.until `yesterday`: expected a date such as 2026-09-30"
    )]
    public void A_bad_value_is_named_and_exits_1(string flag, string value, string message)
    {
        var run = Usage(flag, value);

        Assert.Equal(1, run.Code);
        Assert.Equal($"Error: {message}\n", run.Stderr);
        Assert.Equal("", run.Stdout);
    }

    [Fact]
    public void Daily_and_monthly_together_are_refused()
    {
        var run = Usage("--daily", "--monthly");

        Assert.Equal(2, run.Code);
        Assert.StartsWith(
            "Error: --daily and --monthly cannot be used together\n",
            run.Stderr,
            StringComparison.Ordinal
        );
    }

    private static string Fixture(string host) => RepoPaths.Fixture("usage-logs", host);

    private (int Code, string Stdout, string Stderr) Usage(params string[] flags)
    {
        var config = _home.Write("config.toml", Config());
        return RtokBinary.Exec(
            ["agents", "usage", "--config", config, .. flags],
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["HOME"] = _home.Path,
                ["RTOK_HOME"] = _home.Path,
            }
        );
    }

    /// <summary>rtok's <c>usage-logs.toml</c>: three priced models and every host at the fixtures.</summary>
    private string Config()
    {
        static string Q(string path) => "'" + path + "'";
        string[] hosts =
        [
            "opencode",
            "kilo",
            "copilot",
            "gemini",
            "droid",
            "pi",
            "kimi",
            "grok",
            "zcode",
            "antigravity",
        ];
        return $"""
            [core]
            db_path = {Q(_home.Combine("store", "rtok.db"))}

            [stats]
            transcripts_dir = {Q(Fixture("claude"))}
            codex_dir = {Q(Fixture("codex"))}

            [stats.prices."claude-test"]
            input = 1.0
            cache_write = 1.25
            cache_read = 0.1
            output = 5.0

            [stats.prices."gpt-x"]
            input = 2.0
            cache_write = 2.0
            cache_read = 0.0
            output = 10.0

            [stats.prices."gemini-test"]
            input = 1.0
            cache_write = 1.0
            cache_read = 0.25
            output = 4.0

            [agents.usage.dirs]
            {string.Join('\n', hosts.Select(h => $"{h} = [{Q(Fixture(h))}]"))}
            """;
    }
}
