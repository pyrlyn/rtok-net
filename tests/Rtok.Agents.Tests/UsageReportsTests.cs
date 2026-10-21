using System.Globalization;
using Rtok.Agents.Usage;
using Rtok.Config;

namespace Rtok.Agents.Tests;

public sealed class UsageReportsTests
{
    private const long Now = 1_800_000_000;
    private static readonly long Oct1 = DateTimeOffset
        .Parse("2026-10-01T12:00:00Z", CultureInfo.InvariantCulture)
        .ToUnixTimeSeconds();

    [Fact]
    public void Through_rtok_names_unattributed_requests_and_values_the_saving()
    {
        var config = UsageFixture.Config(u =>
        {
            u.Source = "rtok";
            u.Tz = "UTC";
        });
        var ledger = new FakeLedger(
            [
                Slice("claude", "claude-test", input: 2_000_000),
                Slice(null, "gpt-x", input: 1_000, api: "openai"),
            ],
            [("claude", 1_000_000L), (null, 300L)]
        );

        var report = UsageReports.Build(config, ledger, Now);

        Assert.Equal(
            ["claude", "unattributed (openai)"],
            report.Agents.Select(a => a.Host),
            StringComparer.Ordinal
        );
        Assert.Equal(new UsageSaved(1_000_000, 1.0), report.Agents[0].Saved);
        Assert.Equal(new UsageSaved(0, 2.0 * 0 / 1e6), report.Agents[1].Saved);
        Assert.Equal(new UsageSaved(1_000_300, 1.0), report.Totals.Saved);
        Assert.Equal(
            """
            rtok agents usage: through rtok, up to 2026-10-01 (UTC)

              2M tokens
              $2.00 estimated cost
              2 sessions
              2 daily rows
              rtok saved 1M tokens (≈ $1.00)

            Agent                 Tokens Estimated cost Saved tokens Saved est.
            Claude Code               2M          $2.00           1M      $1.00
            unattributed (openai)     1K          $0.00            0      $0.00

            Monthly totals
            Month   Tokens Estimated cost
            2026-10     2M          $2.00

            """,
            UsageText.Render(report)
        );
    }

    [Fact]
    public void A_host_filter_drops_the_saving_no_session_claimed()
    {
        var config = UsageFixture.Config(u =>
        {
            u.Source = "rtok";
            u.Hosts = ["claude"];
            u.Tz = "UTC";
        });
        var ledger = new FakeLedger(
            [Slice(null, "m")],
            [("claude", 10L), ("codex", 20L), (null, 300L)]
        );

        var report = UsageReports.Build(config, ledger, Now);

        Assert.Empty(report.Agents);
        Assert.Equal(new UsageSaved(10, null), report.Totals.Saved);
    }

    [Fact]
    public void Both_lists_an_agent_seen_only_through_rtok_and_its_coverage()
    {
        var config = UsageFixture.Config(u =>
        {
            u.Source = "both";
            u.Hosts = ["codex", "cursor"];
            u.Tz = "UTC";
        });
        var ledger = new FakeLedger([
            Slice("cursor", "m", input: 10),
            Slice("codex", "gpt-x", input: 1_600_000),
        ]);

        var report = UsageReports.Build(config, ledger, Now);

        var codex = report.Agents.Single(a => a.Host is "codex");
        Assert.Equal((1_600_000L, 0.5), (codex.ThroughRtokTokens, codex.Coverage));
        var cursor = report.Agents.Single(a => a.Host is "cursor");
        Assert.Equal(
            (0L, 10L, (double?)null),
            (cursor.Row.Tokens, cursor.ThroughRtokTokens, cursor.Coverage)
        );
        Assert.Contains(
            "Agent      Tokens Estimated cost Through rtok Coverage Saved tokens Saved est.\n"
                + "Codex        3.2M          $6.00         1.6M      50%            0      $0.00\n"
                + "Cursor CLI      0              -           10        -            0          -\n",
            UsageText.Render(report),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_window_cuts_whole_days_in_the_zone()
    {
        var september = UsageReports.Build(
            UsageFixture.Config(u =>
            {
                u.Hosts = ["claude"];
                u.Tz = "UTC";
                u.Until = "2026-09-30";
            }),
            NoLedger.Instance,
            Now
        );
        var october = UsageReports.Build(
            UsageFixture.Config(u =>
            {
                u.Hosts = ["claude"];
                u.Tz = "UTC";
                u.Since = "2026-10-01";
            }),
            NoLedger.Instance,
            Now
        );

        Assert.Equal(
            ("2026-09", 3_100_000L),
            (september.Periods.Single().Period, september.Totals.Row.Tokens)
        );
        Assert.Equal(
            ("2026-10", 550_000L),
            (october.Periods.Single().Period, october.Totals.Row.Tokens)
        );
        Assert.Empty(october.Skipped);
    }

    [Fact]
    public void The_23_and_25_hour_days_keep_every_hour_in_the_zone()
    {
        Assert.SkipWhen(UsageFixture.Zone("Europe/Kyiv") is null, "no IANA zones here");
        var config = UsageFixture.Config(u =>
        {
            u.Source = "rtok";
            u.Period = "daily";
            u.Tz = "Europe/Kyiv";
        });
        string[] instants =
        [
            "2026-03-28T21:59:59Z",
            "2026-03-28T22:00:00Z",
            "2026-03-29T20:59:59Z",
            "2026-03-29T21:00:00Z",
            "2026-10-24T20:59:59Z",
            "2026-10-24T21:00:00Z",
            "2026-10-25T21:59:59Z",
            "2026-10-25T22:00:00Z",
        ];
        var ledger = new FakeLedger([
            .. instants.Select(at => new UsageSlice(
                "claude",
                "anthropic",
                "s",
                "m",
                DateTimeOffset.Parse(at, CultureInfo.InvariantCulture).ToUnixTimeSeconds(),
                1,
                0,
                0,
                0
            )),
        ]);

        var periods = UsageReports.Build(config, ledger, Now).Periods;

        Assert.Equal(
            [
                ("2026-03-28", 1L),
                ("2026-03-29", 2L),
                ("2026-03-30", 1L),
                ("2026-10-24", 1L),
                ("2026-10-25", 2L),
                ("2026-10-26", 1L),
            ],
            periods.Select(p => (p.Period, p.Row.Tokens))
        );
    }

    [Fact]
    public void A_row_without_a_price_sorts_below_a_free_one()
    {
        var config = UsageFixture.Config(u =>
        {
            u.Source = "rtok";
            u.By = "model";
            u.Tz = "UTC";
        });
        config.Stats.Prices["free"] = UsageFixture.Price(0, 0, 0, 0);
        var ledger = new FakeLedger([
            Slice("claude", "nameless", input: 9_000),
            Slice("claude", "free", input: 5),
            Slice("claude", "gpt-x", input: 1),
        ]);

        var models = UsageReports.Build(config, ledger, Now).Models!;

        Assert.Equal(
            ["gpt-x", "free", "nameless"],
            models.Select(m => m.Model),
            StringComparer.Ordinal
        );
        Assert.Null(models[2].Row.CostUsd);
    }

    [Theory]
    [InlineData("claude-test")]
    [InlineData("anthropic/claude-test")]
    [InlineData("claude-test-20260101")]
    [InlineData("anthropic/claude-test-20260101")]
    public void A_price_is_found_without_the_provider_or_the_date(string model) =>
        Assert.Equal(1.25, UsageCost.Price(UsageFixture.Config().Stats.Prices, model)?.CacheWrite);

    [Theory]
    [InlineData("claude-test-2026010")]
    [InlineData("claude-test-2026010x")]
    [InlineData("other/claude")]
    public void A_near_miss_has_no_price(string model) =>
        Assert.Null(UsageCost.Price(UsageFixture.Config().Stats.Prices, model));

    [Fact]
    public void A_cost_is_rounded_to_a_millionth() =>
        Assert.Equal(0.000002, UsageCost.RowCost(1, 0, 0, 0, UsageFixture.Price(1.5, 0, 0, 0)));

    [Fact]
    public void A_bad_source_fails_before_anything_is_read() =>
        Assert.Throws<ConfigException>(() =>
            UsageReports.Build(UsageFixture.Config(u => u.Source = "all"), new FakeLedger(), Now)
        );

    private static UsageSlice Slice(
        string? host,
        string model,
        long input = 1,
        string api = "anthropic"
    ) => new(host, api, $"{host}-{model}", model, Oct1, input, 0, 0, 0);
}
