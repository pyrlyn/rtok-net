using System.Text.Json.Nodes;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Core.Tests;

public sealed class DispatcherTests
{
    private static readonly PreToolUse Bash = new("Bash", Json.Parse("""{"command": "ls"}"""));
    private static readonly PostToolUse Ran = new("Bash", Json.Parse("{}"), Json.Parse("{}"));
    private static readonly SessionStart Startup = new("startup");

    [Fact]
    public void The_first_deny_wins_and_stops_the_rest()
    {
        var ranLast = false;
        var decision = Dispatch(
                new MemoryHost(),
                new FakePlugin("a") { PreTool = _ => Rewrite("a") },
                new FakePlugin("b") { PreTool = _ => new PreToolDecision.Deny("b") },
                new FakePlugin("c") { PreTool = _ => new PreToolDecision.Deny("c") },
                new FakePlugin("d")
                {
                    PreTool = _ =>
                    {
                        ranLast = true;
                        return Rewrite("d");
                    },
                }
            )
            .PreTool(Bash);
        Assert.Equal(new PreToolDecision.Deny("b"), decision);
        Assert.False(ranLast);
    }

    [Fact]
    public void The_last_rewrite_wins()
    {
        var decision = Dispatch(
                new MemoryHost(),
                new FakePlugin("a") { PreTool = _ => Rewrite("a") },
                new FakePlugin("b"),
                new FakePlugin("c") { PreTool = _ => Rewrite("c") }
            )
            .PreTool(Bash);
        Assert.Equal("c", Assert.IsType<PreToolDecision.Rewrite>(decision).Reason);
    }

    [Fact]
    public void No_decision_is_null() =>
        Assert.Null(Dispatch(new MemoryHost(), new FakePlugin("a")).PreTool(Bash));

    [Fact]
    public void A_disabled_plugin_does_not_run()
    {
        var host = new MemoryHost("""{"plugins": {"a": {"enabled": false}}}""");
        var dispatcher = Dispatch(
            host,
            new FakePlugin("a") { PreTool = _ => new PreToolDecision.Deny("a") }
        );
        Assert.Null(dispatcher.PreTool(Bash));
    }

    [Fact]
    public void A_throwing_plugin_loses_only_its_own_answer()
    {
        var host = new MemoryHost();
        var compacted = false;
        var dispatcher = Dispatch(
            host,
            new FakePlugin("boom")
            {
                PreTool = _ => throw new InvalidOperationException("pre"),
                PostTool = _ => throw new InvalidOperationException("post"),
                SessionStart = _ => throw new InvalidOperationException("start"),
                PreCompact = () => throw new InvalidOperationException("compact"),
            },
            new FakePlugin("ok")
            {
                PreTool = _ => Rewrite("ok"),
                PostTool = _ => "context",
                SessionStart = _ => new Injection("ok", "hello", 1),
                PreCompact = () => compacted = true,
            }
        );
        Assert.Equal("ok", Assert.IsType<PreToolDecision.Rewrite>(dispatcher.PreTool(Bash)).Reason);
        Assert.Equal("context", dispatcher.PostTool(Ran));
        Assert.Equal("hello", dispatcher.SessionStart(Startup));
        dispatcher.PreCompact(new PreCompact("auto", ""));
        Assert.True(compacted);
        Assert.Equal(
            [
                "error plugin/boom: PreToolUse threw: pre",
                "error plugin/boom: PostToolUse threw: post",
                "error plugin/boom: SessionStart threw: start",
                "error plugin/boom: PreCompact threw: compact",
            ],
            host.Logged()
        );
    }

    [Fact]
    public void Post_tool_context_is_joined_in_dispatch_order()
    {
        var dispatcher = Dispatch(
            new MemoryHost(),
            new FakePlugin("a") { PostTool = _ => "one" },
            new FakePlugin("b"),
            new FakePlugin("c") { PostTool = _ => "two" }
        );
        Assert.Equal("one\ntwo", dispatcher.PostTool(Ran));
        Assert.Equal("", Dispatch(new MemoryHost(), new FakePlugin("a")).PostTool(Ran));
    }

    [Fact]
    public void Post_tool_context_stays_under_the_inject_budget()
    {
        var host = new MemoryHost("""{"plugins": {"inject": {"budget_tokens": 30}}}""");
        var huge = string.Join(' ', Enumerable.Repeat("word", 400));
        var dispatcher = Dispatch(
            host,
            new FakePlugin("a") { PostTool = _ => "ok" },
            new FakePlugin("b") { PostTool = _ => huge }
        );
        var context = dispatcher.PostTool(Ran);
        Assert.StartsWith("ok\n", context, StringComparison.Ordinal);
        Assert.Contains("dropped:post_tool:", context, StringComparison.Ordinal);
        Assert.True(host.Estimate(context, TextClass.Prose) <= 30, context);
    }

    [Fact]
    public void Injections_go_in_by_priority_then_plugin_id()
    {
        var host = new MemoryHost();
        var dispatcher = Dispatch(
            host,
            new FakePlugin("low") { SessionStart = _ => new Injection("low", "L", 1) },
            new FakePlugin("zed") { SessionStart = _ => new Injection("zed", "Z", 5) },
            new FakePlugin("abc") { SessionStart = _ => new Injection("abc", "A", 5) }
        );
        Assert.Equal("A\nZ\nL", dispatcher.SessionStart(Startup));
        var row = Assert.Single(host.Recorded());
        Assert.Equal(("inject", "inject"), (row.Plugin, row.Kind));
    }

    [Fact]
    public void No_injection_offered_records_nothing()
    {
        var host = new MemoryHost();
        Assert.Equal("", Dispatch(host, new FakePlugin("a")).SessionStart(Startup));
        Assert.Empty(host.Recorded());
    }

    private static Dispatcher Dispatch(MemoryHost host, params IPlugin[] plugins) =>
        new(new PluginRegistry(plugins, host.ConfigJson), host);

    private static PreToolDecision.Rewrite Rewrite(string reason) =>
        new(JsonNode.Parse("""{"command": "ls -1"}""")!, reason);
}
