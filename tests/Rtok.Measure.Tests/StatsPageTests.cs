using Rtok.Config;
using Rtok.Store;
using Rtok.Testing;
using static Rtok.Measure.Tests.Lines;

namespace Rtok.Measure.Tests;

public sealed class StatsPageTests : IDisposable
{
    private readonly TempDir _home = new();
    private readonly TempDir _logs = new();

    public void Dispose()
    {
        _home.Dispose();
        _logs.Dispose();
    }

    private RtokConfig Config(Action<StatsSection>? stats = null)
    {
        var c = new RtokConfig();
        c.Core.DbPath = _home.Combine("rtok.db");
        c.Core.ArchiveDir = _home.Combine("archive");
        c.Stats.TranscriptsDir = _logs.Path;
        c.Stats.CodexDir = _home.Combine("codex");
        stats?.Invoke(c.Stats);
        return c;
    }

    private string Render(RtokConfig config, StatsRequest? request = null) =>
        StatsPage.Render(
            config,
            _home.Path,
            request ?? new StatsRequest(),
            StatsSignals.None,
            DateTime.UtcNow
        );

    [Theory]
    [InlineData("7x")]
    [InlineData("d")]
    [InlineData("-1d")]
    [InlineData("")]
    [InlineData("99999999999999999d")]
    public void A_bad_since_names_its_source(string bad)
    {
        var flag = Assert.Throws<InvalidOperationException>(() =>
            StatsPage.ParseSince(bad, "--since")
        );
        Assert.StartsWith("--since:", flag.Message, StringComparison.Ordinal);
        var key = Assert.Throws<InvalidOperationException>(() =>
            Render(Config(s => s.Since = bad))
        );
        Assert.StartsWith("stats.since:", key.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Since_takes_days_and_hours()
    {
        Assert.Equal(TimeSpan.FromDays(60), StatsPage.ParseSince("60d", "--since"));
        Assert.Equal(TimeSpan.FromHours(12), StatsPage.ParseSince("12h", "--since"));
    }

    [Fact]
    public void The_report_renders_as_a_table_or_json()
    {
        Write(_logs, "s.jsonl", [Use("t1", "Bash", Obj(("command", "ls"))), Result("t1", "a b c")]);
        Assert.StartsWith("sessions 1  compact 0", Render(Config()), StringComparison.Ordinal);
        var json = Render(Config(s => s.Format = "json"));
        Assert.StartsWith("{", json, StringComparison.Ordinal);
        Assert.EndsWith("}\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_saved_baseline_is_compared_by_flag_or_config()
    {
        Write(_logs, "s.jsonl", [Use("t1", "Bash", Obj(("command", "ls"))), Result("t1", "a b c")]);
        var path = Render(Config(), new StatsRequest { SaveBaseline = "b0" });
        Assert.Equal(_home.Combine("measurements", "b0.json") + "\n", path);
        var byFlag = Render(Config(), new StatsRequest { Compare = "b0" });
        Assert.StartsWith("sessions 1 Δ0\n", byFlag, StringComparison.Ordinal);
        Assert.Equal(byFlag, Render(Config(s => s.Baseline = " b0 ")));
    }

    [Fact]
    public void A_catalogue_plugin_lists_its_measurements()
    {
        using (var store = RtokStore.Open(new StoreOptions(_home.Combine("rtok.db"))))
        {
            store.InsertMeasurement(
                "s1",
                new MeasurementInput("archive", "pointer", 1000, 80, 250, 20, "ab12")
            );
            store.InsertMeasurement(
                "s1",
                new MeasurementInput("archive", "expand", 0, 1000, 0, 250, "ab12")
            );
        }
        var json = Render(Config(s => s.Plugin = "archive"));
        Assert.Contains("\"archive_hits\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"ref_id\": \"ab12\"", json, StringComparison.Ordinal);
        Assert.Contains("\"expand_rate\": 0", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Cache_health_reads_the_store()
    {
        Assert.Contains(
            "no usage rows",
            Render(Config(), new StatsRequest { Cache = true }),
            StringComparison.Ordinal
        );
        Assert.Equal(
            "[]\n",
            Render(Config(s => s.Format = "json"), new StatsRequest { Cache = true })
        );
    }

    [Fact]
    public void Price_adds_the_cost_table()
    {
        using (var store = RtokStore.Open(new StoreOptions(_home.Combine("rtok.db"))))
        {
            store.UpsertSession("s1", null, null, null, "proxy");
            var call = store.InsertCall("s1", "proxy", "api_request");
            store.InsertUsage(
                "s1",
                "claude-sonnet-5",
                "anthropic",
                2_000_000,
                400_000,
                8_000_000,
                500_000,
                call
            );
        }
        Assert.DoesNotContain("11.60", Render(Config()), StringComparison.Ordinal);
        Assert.Contains("11.60", Render(Config(s => s.Price = true)), StringComparison.Ordinal);
    }
}
