using Microsoft.EntityFrameworkCore;
using Rtok.Config;
using Rtok.Store;
using Rtok.Store.Model;
using Rtok.Testing;
using static Rtok.Measure.Tests.Lines;

namespace Rtok.Measure.Tests;

/// <summary>The report rows that come from the store: api, cost, checkpoint notes, cmd.</summary>
public sealed class StatsStoreTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly RtokStore _store;

    public StatsStoreTests()
    {
        _store = RtokStore.Open(
            new StoreOptions(_dir.Combine("rtok.db")) { ArchiveDir = _dir.Combine("archive") }
        );
        _store.UpsertSession("s1", null, null, null, "proxy");
    }

    public void Dispose()
    {
        _store.Dispose();
        _dir.Dispose();
    }

    private void Usage(string? model, string api, long input, long create, long read, long output)
    {
        var call = _store.InsertCall("s1", "proxy", "api_request");
        _store.InsertUsage("s1", model, api, input, create, read, output, call);
    }

    private static ModelPrice Price(double input, double write, double read, double output) =>
        new()
        {
            Input = input,
            CacheWrite = write,
            CacheRead = read,
            Output = output,
        };

    [Fact]
    public void Price_arithmetic_costs_legs_and_cache_saving()
    {
        var p = Price(2.0, 2.5, 0.2, 10.0);
        Assert.Equal((11.6, 14.4), StatsAttach.RowCost(2_000_000, 400_000, 8_000_000, 500_000, p));
        Assert.Equal((0.0, 0.0), StatsAttach.RowCost(0, 0, 0, 0, p));
        // A read price above input never yields a negative saving.
        Assert.Equal((2.0, 0.0), StatsAttach.RowCost(0, 0, 1_000_000, 0, Price(1, 1, 2, 1)));
    }

    [Fact]
    public void Costs_price_known_models_and_dash_unknown_ones()
    {
        Usage("claude-sonnet-5", "anthropic", 2_000_000, 400_000, 8_000_000, 500_000);
        Usage("mystery-1", "anthropic", 30, 0, 0, 1);
        Usage(null, "anthropic", 7, 0, 0, 0);
        var report = new StatsReport();

        StatsAttach.Costs(report, _store, new RtokConfig().Stats.Prices);

        var cost = report.Cost!;
        Assert.Equal((11.6, 14.4), (cost.TotalCost, cost.TotalSaved));
        Assert.Equal(["mystery-1", "unknown"], cost.Unknown);
        var m = cost.Models["mystery-1"];
        Assert.Equal((30L, (double?)null, (double?)null), (m.Input, m.Cost, m.Saved));
        Assert.Equal(11.6, cost.Models["claude-sonnet-5"].Cost);
        var table = StatsTable.Render(report);
        Assert.Contains("11.60", table, StringComparison.Ordinal);
        Assert.Contains("mystery-1", table, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_apis_print_as_two_table_rows()
    {
        Usage("claude-sonnet-5", "anthropic", 100, 0, 50, 10);
        Usage("gpt-5", "openai_chat", 200, 0, 0, 20);
        var report = new StatsReport();

        StatsAttach.Api(report, _store);

        var rows = StatsTable
            .Render(report)
            .Split('\n')
            .Where(l =>
                l.StartsWith("anthropic", StringComparison.Ordinal)
                || l.StartsWith("openai_chat", StringComparison.Ordinal)
            )
            .ToList();
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Checkpoint_notes_split_sessions_with_and_without()
    {
        using var logs = new TempDir();
        Raw(logs, "has.jsonl", """{"type":"user","message":{"content":"a"}}""");
        Raw(logs, "miss.jsonl", """{"type":"user","message":{"content":"b"}}""");
        var r = Collect(logs);
        Assert.Equal((2L, 0L, 2L), (r.Sessions, r.Checkpoint, r.NoCheckpoint));
        using (var db = Db())
        {
            db.Notes.Add(
                new Note
                {
                    Project = "rtok",
                    Title = "checkpoint:has",
                    Kind = "compact",
                    Body = "checkpoint\n",
                }
            );
            db.SaveChanges();
        }

        StatsAttach.CheckpointNotes(r, _store);

        Assert.Equal((1L, 1L), (r.Checkpoint, r.NoCheckpoint));
        Assert.StartsWith(
            "sessions 2  compact 0  checkpoint 1  no_checkpoint 1",
            StatsTable.Render(r),
            StringComparison.Ordinal
        );
    }

    /// <summary>Unmarked <c>cmd</c> bytes split into below the size gate and no rule matched; a
    /// real shrink counts as neither.</summary>
    [Fact]
    public void Bash_unmarked_splits_gate_from_unmatched()
    {
        static MeasurementInput M(string kind, long before, long after) =>
            new("cmd", kind, before, after, 10, 10);
        _store.InsertMeasurement("s1", M("raw", 50, 50));
        _store.InsertMeasurement("s1", M("raw", 30, 30));
        _store.InsertMeasurement("s1", M("unmatched", 900, 900));
        _store.InsertMeasurement("s1", M("rule", 1000, 100));
        var report = new StatsReport();

        StatsAttach.BashCmd(report, _store, StatsSignals.None);

        var d = report.BashUnmarked;
        Assert.Equal(
            (2L, 80L, 1L, 900L),
            (d.GateCalls, d.GateBytes, d.UnmatchedCalls, d.UnmatchedBytes)
        );
        Assert.Contains(
            "bash unmarked  below_gate calls 2 bytes 80",
            StatsTable.Render(report),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Default_rule_savings_need_the_filter_kinds()
    {
        _store.InsertMeasurement(
            "s1",
            new MeasurementInput("cmd", "rule", 1000, 100, 250, 25, "git:status")
        );
        var report = new StatsReport();
        report.BashFamilies.Add("git", 10, 3, 0);
        report.BashFamilies.Finish();

        StatsAttach.BashCmd(report, _store, new StatsSignals { FilterKind = _ => "default" });

        Assert.Equal("default", report.BashFilter["git"]);
        Assert.Equal(100, report.BashDefaultRule.Rows["git"].TotalBytes);
    }

    private RtokDbContext Db() =>
        new(
            new DbContextOptionsBuilder<RtokDbContext>()
                .UseSqlite($"Data Source={_dir.Combine("rtok.db")};Pooling=False")
                .Options
        );
}
