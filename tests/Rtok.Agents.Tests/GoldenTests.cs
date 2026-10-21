using Rtok.Agents.Usage;

namespace Rtok.Agents.Tests;

/// <summary>rtok's <c>agents-usage-*</c> trycmd goldens, over the same fixtures.</summary>
public sealed class GoldenTests
{
    private const long Now = 1_800_000_000;

    [Fact]
    public void The_logs_of_every_host_make_the_default_screen()
    {
        Assert.SkipWhen(UsageFixture.Zone("Europe/Kyiv") is null, "no IANA zones here");
        var report = UsageReports.Build(
            UsageFixture.Config(u => u.Tz = "Europe/Kyiv"),
            NoLedger.Instance,
            Now
        );

        Assert.Equal(
            """
            rtok agents usage: logs from 6 agents, up to 2026-10-02 (Europe/Kyiv)

              6.85M tokens
              $7.70 estimated cost
              6 sessions
              7 daily rows

            ! Cost is incomplete: 3 models have no price in [stats.prices], so their tokens are not in
              the estimate. `rtok agents usage --unpriced` lists them.

            Agent       Tokens Estimated cost
            Codex         3.2M          $6.00
            Claude Code  3.65M          $1.70
            Copilot CLI  1.08K          $0.00
            Gemini CLI     550          $0.00
            pi           1.48K          $0.00
            Kimi Code      865              -

            Monthly totals
            Month   Tokens Estimated cost
            2026-10  6.85M          $7.70

            """,
            UsageText.Render(report)
        );
        var skip = Assert.Single(report.Skipped);
        Assert.Equal(
            (
                "grok",
                "unsupported: xAI documents `grok usage`, not the session files",
                UsageFixture.Dir("grok")
            ),
            (skip.Host, skip.Reason, skip.Path)
        );
    }

    [Fact]
    public void By_model_and_by_day()
    {
        Assert.SkipWhen(UsageFixture.Zone("Europe/Kyiv") is null, "no IANA zones here");
        var config = UsageFixture.Config(u =>
        {
            u.Tz = "Europe/Kyiv";
            u.By = "model";
            u.Period = "daily";
        });

        Assert.Equal(
            """
            rtok agents usage: logs from 6 agents, up to 2026-10-02 (Europe/Kyiv)

              6.85M tokens
              $7.70 estimated cost
              6 sessions
              7 daily rows

            ! Cost is incomplete: 3 models have no price in [stats.prices], so their tokens are not in
              the estimate. `rtok agents usage --unpriced` lists them.

            Model             Tokens Estimated cost
            gpt-x               3.2M          $6.00
            claude-test         3.1M          $1.70
            gemini-test          550          $0.00
            claude-haiku-4-5      50          $0.00
            mystery-model       550K              -
            claude-sonnet-4-5  1.43K              -
            kimi-k2              865              -

            Daily totals
            Day         Tokens Estimated cost
            2026-10-01    6.3M          $7.70
            2026-10-02 552.89K          $0.00

            """,
            UsageText.Render(UsageReports.Build(config, NoLedger.Instance, Now))
        );
    }

    [Fact]
    public void An_empty_store_reads_as_no_usage()
    {
        var config = UsageFixture.Config(u =>
        {
            u.Source = "rtok";
            u.Tz = "UTC";
        });
        var report = UsageReports.Build(config, new FakeLedger(), Now);

        Assert.Equal(
            "rtok agents usage: through rtok, no usage recorded (UTC)\n",
            UsageText.Render(report)
        );
        Assert.Equal(
            """
            {
              "source": "rtok",
              "tz": "UTC",
              "through": null,
              "totals": {
                "tokens": 0,
                "input": 0,
                "cache_write": 0,
                "cache_read": 0,
                "output": 0,
                "cost_usd": null,
                "sessions": 0,
                "daily_rows": 0,
                "saved_tokens": 0,
                "saved_usd": null
              },
              "unpriced_models": 0,
              "unpriced": [],
              "agents": [],
              "periods": [],
              "skipped": []
            }
            """,
            UsageJson.Render(report)
        );
    }

    [Fact]
    public void Both_sources_for_one_host()
    {
        var config = UsageFixture.Config(u =>
        {
            u.Source = "both";
            u.Hosts = ["codex"];
            u.Tz = "UTC";
        });

        Assert.Equal(
            """
            {
              "source": "both",
              "tz": "UTC",
              "through": "2026-10-01",
              "totals": {
                "tokens": 3200000,
                "input": 2000000,
                "cache_write": 0,
                "cache_read": 1000000,
                "output": 200000,
                "cost_usd": 6.0,
                "sessions": 1,
                "daily_rows": 1,
                "saved_tokens": 0,
                "saved_usd": null
              },
              "unpriced_models": 0,
              "unpriced": [],
              "agents": [
                {
                  "host": "codex",
                  "name": "Codex",
                  "tokens": 3200000,
                  "input": 2000000,
                  "cache_write": 0,
                  "cache_read": 1000000,
                  "output": 200000,
                  "cost_usd": 6.0,
                  "through_rtok_tokens": 0,
                  "coverage": 0.0,
                  "saved_tokens": 0,
                  "saved_usd": null
                }
              ],
              "periods": [
                {
                  "period": "2026-10",
                  "tokens": 3200000,
                  "input": 2000000,
                  "cache_write": 0,
                  "cache_read": 1000000,
                  "output": 200000,
                  "cost_usd": 6.0
                }
              ],
              "skipped": []
            }
            """,
            UsageJson.Render(UsageReports.Build(config, new FakeLedger(), Now))
        );
    }
}
