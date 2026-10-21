using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugin.Sdk.Tests;

public sealed class ContractTests
{
    [Fact]
    public void APluginImplementsOnlyWhatItDeclares()
    {
        IPlugin plugin = new Quiet();
        var cx = new Ctx(new MemoryHost());
        using var input = JsonDocument.Parse("{}");
        Assert.Null(plugin.OnPreTool(new PreToolUse("Bash", input.RootElement), cx));
        Assert.Null(
            plugin.OnPostTool(new PostToolUse("Bash", input.RootElement, input.RootElement), cx)
        );
        Assert.Null(plugin.OnSessionStart(new SessionStart("startup"), cx));
        Assert.Null(plugin.OnPromptSubmit(new PromptSubmit("hi"), cx));
        Assert.Null(plugin.OnSubagentStart(new SubagentStart("general-purpose", ""), cx));
        plugin.OnPreCompact(new PreCompact("auto", "/t.jsonl"), cx);
        Assert.Empty(plugin.McpTools);
        Assert.Empty(plugin.ProxyFilter(new WireRequest(new NoResults(), new JsonObject()), cx));
    }

    [Fact]
    public void SurfaceNamesFollowTheCatalogueOrder()
    {
        Assert.Equal(
            ["hook", "mcp", "cli"],
            SurfaceNames.Of(Surface.Cli | Surface.Hook | Surface.Mcp)
        );
        Assert.Empty(SurfaceNames.Of(Surface.None));
    }

    [Fact]
    public void RecentHookInputsForEventFiltersByName()
    {
        ILedger ledger = new TwoInputs();
        Assert.Equal(
            ["""{"hook_event_name":"PostToolUse","n":1}"""],
            ledger.RecentHookInputsForEvent("PostToolUse", 10)
        );
    }

    [Fact]
    public void AJsonSlotWritesBackIntoTheBody()
    {
        var body = JsonNode.Parse("""{"a":[{"content":"long"}]}""")!;
        var item = body["a"]![0]!.AsObject();
        var slot = new JsonSlot(item, "content");
        Assert.Equal("long", slot.Value!.GetValue<string>());
        slot.Value = "short";
        var element = new JsonSlot(body["a"]!.AsArray(), 0);
        Assert.Equal("""{"a":[{"content":"short"}]}""", body.ToJsonString());
        element.Value = 1;
        Assert.Equal("""{"a":[1]}""", body.ToJsonString());
    }

    private sealed class Quiet : IPlugin
    {
        public Manifest Manifest { get; } = new("quiet", Surface.None, DefaultOn: false);

        public DashboardPage DashboardPage { get; } =
            new("Quiet", "Does nothing.", SavesTokens: false);
    }

    private sealed class NoResults : IToolResults
    {
        public IReadOnlyList<ToolResultRef> ToolResults(JsonNode request) => [];
    }

    private sealed class TwoInputs : ILedger
    {
        public IReadOnlyList<string> RecentHookInputs(int limit) =>
            [
                """{"hook_event_name":"PostToolUse","n":1}""",
                """{"hook_event_name":"PreToolUse"}""",
                "not json",
            ];

        public long CallsSince(long ts) => 0;

        public string? LastMeasurementRef(string plugin, string kind) => null;
    }
}
