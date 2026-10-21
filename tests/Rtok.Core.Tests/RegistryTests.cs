using System.Text.Json;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;
using Rtok.Plugins.Cmd;
using Rtok.Plugins.Measure;

namespace Rtok.Core.Tests;

public sealed class RegistryTests
{
    // Every built-in plugin in the repository; each new one is added here.
    private static readonly IPlugin[] BuiltIn = [new MeasurePlugin(), new CmdPlugin()];

    [Fact]
    public void Every_built_in_plugin_matches_the_catalogue()
    {
        var registry = new PluginRegistry(BuiltIn, _ => null);
        foreach (var entry in registry.Entries)
        {
            var manifest = entry.Plugin.Manifest;
            var index = Catalogue.IndexOf(manifest.Id);
            Assert.True(index >= 0, manifest.Id);
            Assert.Equal(Catalogue.Entries[index].DefaultOn, manifest.DefaultOn);
            Assert.Equal("builtin", entry.Kind);
        }
    }

    [Fact]
    public void Catalogue_ids_are_unique() =>
        Assert.Equal(
            Catalogue.Entries.Count,
            Catalogue.Entries.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count()
        );

    [Fact]
    public void Built_in_plugins_run_in_catalogue_order_then_external_ones_in_link_order()
    {
        var registry = new PluginRegistry(
            [
                new FakePlugin("zeta"),
                new FakePlugin("cmd"),
                new FakePlugin("alpha"),
                new MeasurePlugin(),
            ],
            _ => null
        );
        Assert.Equal(
            ["measure", "cmd", "zeta", "alpha"],
            registry.Entries.Select(e => e.Plugin.Manifest.Id),
            StringComparer.Ordinal
        );
        Assert.Equal(
            ["builtin", "builtin", "external", "external"],
            registry.Entries.Select(e => e.Kind),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void Config_overrides_the_default_either_way()
    {
        var host = new MemoryHost(
            """{"plugins": {"measure": {"enabled": false}, "docs": {"enabled": true}}}"""
        );
        var registry = new PluginRegistry(
            [new MeasurePlugin(), new FakePlugin("docs", defaultOn: false), new FakePlugin("cmd")],
            host.ConfigJson
        );
        Assert.Equal(
            [("measure", false), ("cmd", true), ("docs", true)],
            registry.Entries.Select(e => (e.Plugin.Manifest.Id, e.Enabled))
        );
        Assert.Equal(
            ["cmd", "docs"],
            registry.Enabled().Select(p => p.Manifest.Id),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void An_enabled_that_is_not_a_boolean_keeps_the_default()
    {
        using var doc = JsonDocument.Parse("""{"enabled": "no"}""");
        var section = doc.RootElement.Clone();
        var registry = new PluginRegistry([new MeasurePlugin()], _ => section);
        Assert.True(registry.Entries[0].Enabled);
    }
}
