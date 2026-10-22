using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph.Tests;

// The plugin's surface: the manifest, the tool budget, dispatch and the edit hook.
public sealed class GraphPluginTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    [Fact]
    public void Five_tools_stay_under_the_description_budget()
    {
        using var h = new GraphHarness();
        var plugin = new GraphPlugin();
        Assert.Equal(
            new Manifest("graph", Surface.Mcp | Surface.Hook, DefaultOn: true),
            plugin.Manifest
        );
        Assert.Equal(
            ["symbol", "callers", "impact", "outline", "explore"],
            plugin.McpTools.Select(t => t.Name),
            StringComparer.Ordinal
        );
        var costs = plugin
            .McpTools.Select(t => h.Host.Estimate(t.Description, TextClass.Prose))
            .ToList();
        Assert.All(costs, c => Assert.InRange(c, 1, 60));
        Assert.InRange(costs.Sum(), 1, 150);
        Assert.All(
            plugin.McpTools,
            t => Assert.Equal(JsonValueKind.Object, t.InputSchema.ValueKind)
        );
    }

    [Fact]
    public void A_call_answers_its_tool_and_refuses_what_is_missing()
    {
        using var h = new GraphHarness();
        h.Write("chain.rs", GraphToolsTests.Chain);
        var plugin = new GraphPlugin();

        Assert.Null(plugin.CallTool("nope", Json("{}"), h.Cx));
        var e = Assert.Throws<ArgumentException>(() => plugin.CallTool("symbol", Json("{}"), h.Cx));
        Assert.StartsWith("missing `name` or `id`", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => plugin.CallTool("callers", Json("{}"), h.Cx));
        Assert.Equal(
            GraphTools.Symbols(h.Cx, h.Project.Path, ["b", "c"]),
            plugin.CallTool("symbol", Json("""{"names":["b","c"]}"""), h.Cx)
        );
        Assert.Equal(
            "a → b → c\n",
            plugin.CallTool("impact", Json("""{"name":"a","to":"c","depth":3}"""), h.Cx)
        );
        Assert.Equal(
            GraphTools.Callers(h.Cx, h.Project.Path, "c"),
            plugin.CallTool("callers", Json("""{"name":"c"}"""), h.Cx)
        );
        Assert.Equal(
            GraphTools.Explore(h.Cx, h.Project.Path, "b c"),
            plugin.CallTool("explore", Json("""{"query":"b c"}"""), h.Cx)
        );
    }

    [Fact]
    public void An_edit_or_write_marks_its_file_stale()
    {
        using var h = new GraphHarness();
        var file = h.Write("a.rs", "fn alpha() {}\n");
        h.Run();
        var plugin = new GraphPlugin();

        Assert.Null(
            plugin.OnPostTool(
                new PostToolUse(
                    "Read",
                    Json($$"""{"file_path":{{JsonSerializer.Serialize(file)}}}"""),
                    default
                ),
                h.Cx
            )
        );
        Assert.Empty(h.Store.SymbolPending(h.Root, h.Root));
        plugin.OnPostTool(
            new PostToolUse(
                "Edit",
                Json($$"""{"file_path":{{JsonSerializer.Serialize(file)}}}"""),
                default
            ),
            h.Cx
        );
        Assert.Equal(["a.rs"], h.Store.SymbolPending(h.Root, h.Root));
    }

    [Fact]
    public void Five_percent_is_the_common_name_cutoff()
    {
        Assert.False(Resolve.TooCommon(5, 100));
        Assert.True(Resolve.TooCommon(6, 100));
        Assert.False(Resolve.TooCommon(0, 10));
    }
}
