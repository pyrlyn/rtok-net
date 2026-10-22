using Rtok.Measure;

namespace Rtok.Report.Tests;

/// <summary>Each rule fires on its own facts and on nothing else; the list is ordered by
/// recoverable tokens.</summary>
public sealed class AdviceTests
{
    [Fact]
    public void Expand_rate_fires_above_max_rate()
    {
        Assert.Equal(["expand-rate", "archive-window"], Rules(Expand(100, 6)));
        Assert.Equal(["archive-window"], Rules(Expand(100, 5)));
    }

    [Fact]
    public void A_net_saving_at_or_below_zero_retires_the_plugin()
    {
        var facts = Facts.Empty with
        {
            Savings =
            [
                new PluginSaving("graph", 2, 100, 300),
                new PluginSaving("even", 1, 50, 50),
                new PluginSaving("cmd", 3, 900, 100),
                new PluginSaving("idle", 1, 0, 0),
            ],
        };

        var findings = Advice.Of(facts);

        Assert.Equal(
            ["graph", "even"],
            findings.Select(f => f.Text.Split(' ')[1]),
            StringComparer.Ordinal
        );
        Assert.All(findings, f => Assert.Equal("retire-plugin", f.Rule));
        Assert.Equal([200, 0], findings.Select(f => f.Tokens));
    }

    [Fact]
    public void A_tools_or_system_bust_names_its_session_and_cost()
    {
        var facts = Facts.Empty with
        {
            Cache =
            [
                new CacheHealth.Session(
                    "s1",
                    [
                        new CacheHealth.Turn(1, 1, 30_000, 0, null),
                        new CacheHealth.Turn(2, 1, 31_000, 0, "tools"),
                        new CacheHealth.Turn(3, 1, 25_000, 0, "system"),
                        new CacheHealth.Turn(4, 1, 22_000, 0, "ttl"),
                    ]
                ),
            ],
        };

        var findings = Advice.Of(facts);

        Assert.Equal(
            ["cache-bust", "cache-bust"],
            findings.Select(f => f.Rule),
            StringComparer.Ordinal
        );
        Assert.Equal([31_000, 25_000], findings.Select(f => f.Tokens));
        Assert.Contains("cause tools", findings[0].Text, StringComparison.Ordinal);
        Assert.Contains(
            "3 cache busts over 4 turns",
            findings[0].Evidence,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_hook_with_ten_calls_and_no_measurement_is_idle()
    {
        var facts = Facts.Empty with
        {
            Hooks =
            [
                new HookActivity("Stop", Advice.IdleHookCalls, 0),
                new HookActivity("PreToolUse", 40, 1),
                new HookActivity("Notification", Advice.IdleHookCalls - 1, 0),
            ],
        };

        var finding = Assert.Single(Advice.Of(facts));

        Assert.Equal("idle-hook", finding.Rule);
        Assert.StartsWith("Stop fired 10 times", finding.Text, StringComparison.Ordinal);
        Assert.Equal(0, finding.Tokens);
    }

    [Fact]
    public void Inject_over_its_budget_names_the_excess()
    {
        var over = Facts.Empty with { Savings = [new PluginSaving("inject", 2, 9_000, 2_500)] };
        var within = Facts.Empty with { Savings = [new PluginSaving("inject", 2, 9_000, 2_000)] };

        var finding = Assert.Single(Advice.Of(over));

        Assert.Equal("inject-budget", finding.Rule);
        Assert.Equal(500, finding.Tokens);
        Assert.Empty(Advice.Of(within));
    }

    [Fact]
    public void Re_read_pointers_ask_for_a_wider_archive_window()
    {
        var finding = Assert.Single(Advice.Of(Expand(1_000, 1)));

        Assert.Equal("archive-window", finding.Rule);
        Assert.Contains("keep_turns 20", finding.Text, StringComparison.Ordinal);
        Assert.Contains("min_tokens (500)", finding.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Top_sinks_suggest_the_switch_of_their_class()
    {
        var facts = Facts.Empty with
        {
            Sinks =
            [
                new Sink("read", "src/a.cs", 9_000, 3),
                new Sink("mcp", "github", 3_000, 1),
                new Sink("cmd", "git", 2, 1),
            ],
        };

        var findings = Advice.Of(facts);

        Assert.Equal([3_000, 1_000, 1], findings.Select(f => f.Tokens));
        Assert.Contains("default_mode = \"map\"", findings[0].Text, StringComparison.Ordinal);
        Assert.Contains("rtok mcp -- github", findings[1].Text, StringComparison.Ordinal);
        Assert.Contains("rules entry for `git`", findings[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Findings_are_ordered_by_tokens_with_rule_order_breaking_ties()
    {
        var facts = Expand(100, 50) with
        {
            Hooks = [new HookActivity("Stop", 12, 0)],
            Sinks = [new Sink("cmd", "git", 3, 1)],
            Savings = [new PluginSaving("graph", 1, 0, 800)],
        };

        var findings = Advice.Of(facts);

        Assert.Equal(
            ["retire-plugin", "expand-rate", "archive-window", "top-sinks", "idle-hook"],
            findings.Select(f => f.Rule),
            StringComparer.Ordinal
        );
        Assert.True(findings.Zip(findings.Skip(1)).All(p => p.First.Tokens >= p.Second.Tokens));
    }

    [Fact]
    public void An_empty_store_prints_no_recommendations()
    {
        Assert.Empty(Advice.Of(Facts.Empty));
        Assert.Contains(
            Findings.None,
            MarkdownRenderer.Text(Facts.Document(Facts.Empty)),
            StringComparison.Ordinal
        );
    }

    private static ReportFacts Expand(long decisions, long expanded) =>
        Facts.Empty with
        {
            Expand = new ExpandFacts(decisions, expanded, 700, expanded, ["arc1"]),
        };

    private static List<string> Rules(ReportFacts facts) =>
        [.. Advice.Of(facts).Select(f => f.Rule)];
}
