using Rtok.Agents.Usage;

namespace Rtok.Agents.Tests;

public sealed class UsageJsonTests
{
    [Theory]
    [InlineData(6.0, "6.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(0.000015, "0.000015")]
    [InlineData(0.1234, "0.1234")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(1e17, "100000000000000000.0")]
    public void A_double_is_a_plain_decimal_that_keeps_its_point(double v, string text) =>
        Assert.Equal(text, UsageJson.Number(v));

    [Fact]
    public void An_agent_through_rtok_lists_its_fields_in_order()
    {
        var config = UsageFixture.Config(u =>
        {
            u.Source = "both";
            u.Hosts = ["claude"];
            u.Tz = "UTC";
        });
        var ledger = new FakeLedger(
            [
                new UsageSlice(
                    "claude",
                    "anthropic",
                    "s",
                    "claude-test",
                    1_790_900_000,
                    1_000_000,
                    0,
                    0,
                    0
                ),
            ],
            [("claude", 500_000L)]
        );

        var json = UsageJson.Render(UsageReports.Build(config, ledger, 1_800_000_000));

        string[] keys =
        [
            "\"host\": \"claude\"",
            "\"name\": \"Claude Code\"",
            "\"tokens\": 3650000",
            "\"cost_usd\": ",
            "\"through_rtok_tokens\": 1000000",
            "\"coverage\": 0.274,",
            "\"saved_tokens\": 500000",
            "\"saved_usd\": 0.5",
        ];
        var agents = json[json.IndexOf("\"agents\"", StringComparison.Ordinal)..];
        var at = keys.Select(k => agents.IndexOf(k, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, at);
        Assert.Equal([.. at.Order()], at);
        Assert.DoesNotContain("\"models\"", json, StringComparison.Ordinal);
    }
}
