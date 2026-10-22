using System.Buffers;
using System.Text;
using System.Text.Json;
using Rtok.Store;

namespace Rtok.Measure;

/// <summary>
/// <c>rtok stats --plugin &lt;id&gt;</c> for a catalogue plugin: its measurement rows as data.
/// Expand rows stay in the list as a cost; <c>archive_hits</c> counts them apart. For
/// <c>archive</c> the expand rate of the proxy's pointers comes too.
/// </summary>
internal static class PluginPage
{
    public static string Json(RtokStore store, string plugin)
    {
        var rows = store.MeasurementsOf(plugin);
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("plugin", plugin);
            w.WriteNumber("archive_hits", rows.Count(r => r.Kind is "expand"));
            w.WriteStartArray("rows");
            foreach (var r in rows)
            {
                w.WriteStartObject();
                w.WriteString("kind", r.Kind);
                w.WriteNumber("before", r.BeforeBytes);
                w.WriteNumber("after", r.AfterBytes);
                w.WriteNumber("est_before", r.EstBefore);
                w.WriteNumber("est_after", r.EstAfter);
                w.WriteString("ref_id", r.RefId);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            if (plugin is "archive")
            {
                var (decisions, expanded) = store.ArchiveDecisionCounts();
                w.WriteNumber("decisions", decisions);
                w.WriteNumber("expanded", expanded);
                w.WriteNumber("expand_rate", decisions > 0 ? (double)expanded / decisions : 0.0);
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
