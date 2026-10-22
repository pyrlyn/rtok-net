using System.Text;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary>
/// <c>rtok stats</c> end to end on the shared transcript fixture (<c>tests/fixtures/stats</c>)
/// and a store seeded with proxy usage, call bodies and <c>cmd</c> measurements.
/// </summary>
public sealed class StatsCommandTests : IDisposable
{
    private const string Tools =
        """{"system":"You are a coding agent.","tools":[{"name":"Read"}],"messages":[]}""";
    private const string MoreTools =
        """{"system":"You are a coding agent.","tools":[{"name":"Read"},{"name":"Bash"}],"messages":[]}""";

    private readonly TempDir _home = new("rtok-stats-");

    public StatsCommandTests()
    {
        using var store = RtokStore.Open(
            new StoreOptions(_home.Combine("store", "rtok.db"))
            {
                ArchiveDir = _home.Combine("archive"),
            }
        );
        store.UpsertSession("sess-1", null, null, null, "proxy");
        foreach (
            var (body, create, read) in new[]
            {
                (Tools, 30_000L, 0L),
                (Tools, 500L, 30_000L),
                (MoreTools, 31_000L, 200L),
                (MoreTools, 400L, 31_000L),
            }
        )
        {
            var id = store.InsertCall("sess-1", "proxy", "api_request");
            store.InsertCallIo(id, Encoding.UTF8.GetBytes(body), null);
            store.InsertUsage(
                "sess-1",
                "claude-sonnet-5",
                "anthropic",
                1_000,
                create,
                read,
                200,
                id
            );
        }
        store.InsertMeasurement(
            "sess-1",
            new MeasurementInput("cmd", "rule", 4_000, 400, 1_000, 100, "cargo:test")
        );
        store.InsertMeasurement("sess-1", new MeasurementInput("cmd", "raw", 300, 300, 75, 75));
    }

    public void Dispose() => _home.Dispose();

    [Theory]
    [InlineData("table")]
    [InlineData("json", "--json")]
    [InlineData("price", "--price")]
    [InlineData("plugin", "--plugin", "cmd")]
    [InlineData("cache", "--cache")]
    public Task Every_row_on_the_shared_fixture(string name, params string[] flags)
    {
        var run = Stats(flags);

        Assert.Equal((0, ""), (run.Code, run.Stderr));
        return Verify(run.Stdout).UseParameters(name);
    }

    [Fact]
    public void A_saved_baseline_compares_unchanged()
    {
        var saved = Stats("--save-baseline", "a");
        var compared = Stats("--compare", "a");

        Assert.Equal(
            (0, _home.Combine("measurements", "a.json") + "\n"),
            (saved.Code, saved.Stdout)
        );
        Assert.Equal((0, ""), (compared.Code, compared.Stderr));
        var lines = compared.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, l => l.StartsWith("sessions 1 ", StringComparison.Ordinal));
        Assert.All(lines, l => Assert.EndsWith(" Δ0", l, StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_baseline_is_refused()
    {
        var run = Stats("--compare", "nope");

        Assert.Equal((1, ""), (run.Code, run.Stdout));
        Assert.StartsWith("Error: no baseline 'nope' at ", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bad_window_names_the_flag()
    {
        var run = Stats("--since", "soon");

        Assert.Equal(1, run.Code);
        Assert.StartsWith(
            "Error: flag --since: stats.since: ",
            run.Stderr,
            StringComparison.Ordinal
        );
    }

    private (int Code, string Stdout, string Stderr) Stats(params string[] flags)
    {
        static string Q(string path) => "'" + path + "'";
        var config = _home.Write(
            "config.toml",
            $"""
            [core]
            db_path = {Q(_home.Combine("store", "rtok.db"))}
            archive_dir = {Q(_home.Combine("archive"))}

            [stats]
            since = "36500d"
            transcripts_dir = {Q(RepoPaths.Fixture("stats", "claude"))}
            codex_dir = {Q(_home.Combine("codex"))}
            """
        );
        return RtokBinary.Exec(
            ["stats", "--config", config, .. flags],
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["HOME"] = _home.Path,
                ["RTOK_HOME"] = _home.Path,
            }
        );
    }
}
