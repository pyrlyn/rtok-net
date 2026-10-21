using Rtok.Config;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Model;

/// <summary>The Plugins page: every linked plugin, enabled as the config says, with its
/// dashboard page.</summary>
/// <param name="linked">The plugins this build links.</param>
public sealed class PluginsSource(Func<IEnumerable<IPlugin>> linked)
    : IPageSource<IReadOnlyList<PluginRow>>
{
    /// <inheritdoc />
    public IReadOnlyList<PluginRow> Read(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var registry = new PluginRegistry(linked(), key => ConfigJson.Find(context.Config, key));
        return [.. registry.Entries.Select(Row)];
    }

    private static PluginRow Row(RegisteredPlugin entry)
    {
        var plugin = entry.Plugin;
        var page = plugin.DashboardPage;
        return new PluginRow(
            plugin.Manifest.Id,
            entry.Kind,
            entry.Enabled,
            SurfaceNames.Of(plugin.Manifest.Surfaces),
            page.Title,
            page.Summary,
            page.SavesTokens,
            [.. page.Fields.Select(f => new PluginField(f.Key, f.Value))]
        );
    }
}
