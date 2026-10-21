using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Core;

/// <summary>
/// The plugins this build links (N12), in dispatch order: built-in ones in
/// <see cref="Catalogue"/> order, then external ones in link order.
/// </summary>
public sealed class PluginRegistry
{
    /// <summary>Registers <paramref name="linked"/>. <paramref name="configJson"/> answers a
    /// dotted config path the way <see cref="IHost.ConfigJson"/> does; a plugin runs when
    /// <c>plugins.&lt;id&gt;.enabled</c> says so, else when its manifest is on by default.</summary>
    public PluginRegistry(IEnumerable<IPlugin> linked, Func<string, JsonElement?> configJson)
    {
        ArgumentNullException.ThrowIfNull(linked);
        ArgumentNullException.ThrowIfNull(configJson);
        var all = linked.ToList();
        var builtin = all.Where(p => Catalogue.IndexOf(p.Manifest.Id) >= 0)
            .OrderBy(p => Catalogue.IndexOf(p.Manifest.Id));
        var external = all.Where(p => Catalogue.IndexOf(p.Manifest.Id) < 0);
        Entries =
        [
            .. builtin.Select(p => new RegisteredPlugin(p, "builtin", IsEnabled(p, configJson))),
            .. external.Select(p => new RegisteredPlugin(p, "external", IsEnabled(p, configJson))),
        ];
    }

    /// <summary>Every linked plugin, enabled or not, in dispatch order.</summary>
    public IReadOnlyList<RegisteredPlugin> Entries { get; }

    /// <summary>The plugins that run, in dispatch order.</summary>
    public IEnumerable<IPlugin> Enabled() => Entries.Where(e => e.Enabled).Select(e => e.Plugin);

    private static bool IsEnabled(IPlugin plugin, Func<string, JsonElement?> configJson) =>
        configJson("plugins." + plugin.Manifest.Id) is { ValueKind: JsonValueKind.Object } section
        && section.TryGetProperty("enabled", out var enabled)
        && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? enabled.GetBoolean()
            : plugin.Manifest.DefaultOn;
}
