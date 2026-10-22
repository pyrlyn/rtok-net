using System.Text.Json;
using Rtok.Core;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugins.Docs.Tests;

public sealed class DocsPluginTests
{
    [Fact]
    public void Off_by_default_so_no_docs_tool_is_listed()
    {
        var registry = new PluginRegistry([new DocsPlugin()], _ => null);

        Assert.Empty(registry.Enabled().SelectMany(p => p.McpTools));
    }

    [Fact]
    public void Enabled_it_lists_the_three_tools()
    {
        using var on = JsonDocument.Parse("""{"enabled":true}""");
        var registry = new PluginRegistry(
            [new DocsPlugin()],
            path =>
                string.Equals(path, "plugins.docs", StringComparison.Ordinal)
                    ? on.RootElement
                    : null
        );

        Assert.Equal(
            ["docs_resolve", "docs_query", "docs_get"],
            registry.Enabled().SelectMany(p => p.McpTools).Select(t => t.Name),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void The_tool_descriptions_stay_within_sixty_tokens()
    {
        var host = new MemoryHost();

        Assert.InRange(
            new DocsPlugin().McpTools.Sum(t => host.Estimate(t.Description, TextClass.Prose)),
            1,
            60
        );
    }

    [Fact]
    public void A_snippet_counts_characters_not_utf16_units() =>
        Assert.Equal("a😀b", DocsService.Prefix("a😀bc", 3));
}
