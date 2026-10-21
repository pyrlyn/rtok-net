using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Cli;

/// <summary>The <c>rtok plugins</c> output: a table of id, kind, enabled and surfaces, or JSON
/// with each plugin's page as well.</summary>
internal static class PluginsTable
{
    public static string Render(PluginRegistry registry)
    {
        var text = new StringBuilder();
        text.Append(Row("id", "kind", "enabled", "surfaces"));
        foreach (var entry in registry.Entries)
        {
            var manifest = entry.Plugin.Manifest;
            text.Append(
                Row(
                    manifest.Id,
                    entry.Kind,
                    entry.Enabled ? "on" : "off",
                    string.Join(',', SurfaceNames.Of(manifest.Surfaces))
                )
            );
        }

        return text.ToString();
    }

    public static string RenderJson(PluginRegistry registry)
    {
        using var buffer = new MemoryStream();
        using (
            var json = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Indented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            )
        )
        {
            json.WriteStartArray();
            foreach (var entry in registry.Entries)
            {
                var manifest = entry.Plugin.Manifest;
                var page = entry.Plugin.DashboardPage;
                json.WriteStartObject();
                json.WriteString("id", manifest.Id);
                json.WriteString("kind", entry.Kind);
                json.WriteBoolean("enabled", entry.Enabled);
                json.WriteStartArray("surfaces");
                foreach (var surface in SurfaceNames.Of(manifest.Surfaces))
                {
                    json.WriteStringValue(surface);
                }

                json.WriteEndArray();
                json.WriteString("title", page.Title);
                json.WriteString("summary", page.Summary);
                json.WriteBoolean("saves_tokens", page.SavesTokens);
                json.WriteStartArray("fields");
                foreach (var (name, value) in page.Fields)
                {
                    json.WriteStartArray();
                    json.WriteStringValue(name);
                    json.WriteStringValue(value);
                    json.WriteEndArray();
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    private static string Row(string id, string kind, string enabled, string surfaces) =>
        id.PadRight(10) + kind.PadRight(10) + enabled.PadRight(10) + surfaces + "\n";
}
