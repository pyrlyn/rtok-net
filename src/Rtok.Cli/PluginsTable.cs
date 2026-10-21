using System.Text;
using Rtok.Plugin.Sdk;

namespace Rtok.Cli;

/// <summary>The <c>rtok plugins</c> table: id, enabled, surfaces, as rtok prints it.</summary>
internal static class PluginsTable
{
    public static string Render(IEnumerable<IPlugin> plugins)
    {
        var text = new StringBuilder();
        text.Append(Row("id", "enabled", "surfaces"));
        foreach (var plugin in plugins)
        {
            var manifest = plugin.Manifest;
            text.Append(
                Row(
                    manifest.Id,
                    manifest.DefaultOn ? "on" : "off",
                    string.Join(',', SurfaceNames.Of(manifest.Surfaces))
                )
            );
        }

        return text.ToString();
    }

    private static string Row(string id, string enabled, string surfaces) =>
        id.PadRight(10) + enabled.PadRight(10) + surfaces + "\n";
}
