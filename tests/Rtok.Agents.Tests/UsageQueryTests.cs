using Rtok.Agents.Usage;
using Rtok.Config;

namespace Rtok.Agents.Tests;

public sealed class UsageQueryTests
{
    [Theory]
    [InlineData(
        "source",
        "proxy",
        "agents.usage.source `proxy`: expected `logs`, `rtok` or `both`"
    )]
    [InlineData("period", "weekly", "agents.usage.period `weekly`: expected `monthly` or `daily`")]
    [InlineData("by", "host", "agents.usage.by `host`: expected `agent` or `model`")]
    [InlineData(
        "until",
        "2026-02-30",
        "agents.usage.until `2026-02-30`: expected a date such as 2026-09-30"
    )]
    public void A_value_out_of_range_is_named(string key, string value, string message)
    {
        var usage = new AgentsUsageSection();
        switch (key)
        {
            case "source":
                usage.Source = value;
                break;
            case "period":
                usage.Period = value;
                break;
            case "by":
                usage.By = value;
                break;
            default:
                usage.Until = value;
                break;
        }

        Assert.Equal(
            message,
            Assert.Throws<ConfigException>(() => UsageQuery.Of(usage, 0)).Message
        );
    }

    [Fact]
    public void An_unknown_host_lists_the_known_ones()
    {
        var usage = new AgentsUsageSection { Hosts = ["claude", "clippy"] };

        var e = Assert.Throws<ConfigException>(() => UsageQuery.Of(usage, 0));

        Assert.StartsWith(
            "unknown host `clippy` in agents.usage.hosts; known: claude, cursor,",
            e.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("droid", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Droid_is_a_known_host() =>
        Assert.True(
            UsageQuery
                .Of(new AgentsUsageSection { Hosts = ["droid"], Tz = "UTC" }, 0)
                .Wants("droid")
        );
}
