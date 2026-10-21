using System.Text.Json;

namespace Rtok.Process;

/// <summary>
/// One JSON object per line, <c>{"progress":{"pos":3,"total":10,"unit":"items","msg":"x"}}</c>:
/// how rtok's own child processes and plugin CLIs report. <c>total</c> and <c>msg</c> are
/// optional; <c>unit</c> is <c>items</c> (the default), <c>bytes</c> or <c>percent</c>. Any other
/// line, or an unknown unit, is ignored.
/// </summary>
public sealed class JsonLinesProgress : IProgressParser
{
    /// <inheritdoc/>
    public ProgressUpdate? Feed(ChildOutput stream, string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        line = line.Trim();
        if (!line.StartsWith('{'))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(line);
            return Read(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ProgressUpdate? Read(JsonElement root)
    {
        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("progress", out var p)
            || p.ValueKind != JsonValueKind.Object
            || !p.TryGetProperty("pos", out var pos)
            || pos.ValueKind != JsonValueKind.Number
            || !pos.TryGetUInt64(out var position)
        )
        {
            return null;
        }
        ProgressUnit? unit = p.TryGetProperty("unit", out var u)
            ? u.ValueKind == JsonValueKind.String
                ? u.GetString() switch
                {
                    "items" => ProgressUnit.Items,
                    "bytes" => ProgressUnit.Bytes,
                    "percent" => ProgressUnit.Percent,
                    _ => null,
                }
                : null
            : ProgressUnit.Items;
        if (unit is null)
        {
            return null;
        }
        long? total =
            p.TryGetProperty("total", out var t)
            && t.ValueKind == JsonValueKind.Number
            && t.TryGetUInt64(out var count)
                ? (long)count
                : null;
        var message =
            p.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
        return new ProgressUpdate((long)position, total, unit.Value, message);
    }
}
