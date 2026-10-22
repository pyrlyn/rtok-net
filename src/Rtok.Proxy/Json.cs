using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rtok.Proxy;

/// <summary>Reads that never throw on a body of the wrong shape: a provider body is data.</summary>
internal static class Json
{
    /// <summary>The parsed document, or null when <paramref name="bytes"/> is not JSON.</summary>
    public static JsonDocument? Parse(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The body as a mutable object, or null when it is not a JSON object.</summary>
    public static JsonObject? ParseObject(byte[] bytes)
    {
        try
        {
            return JsonNode.Parse(bytes) as JsonObject;
        }
        catch (Exception e)
            when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A rewritten body: compact, with non-ASCII text left as the client sent it.</summary>
    public static byte[] Write(JsonNode node)
    {
        using var buffer = new MemoryStream();
        using (
            var writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
            )
        )
        {
            node.WriteTo(writer);
        }
        return buffer.ToArray();
    }

    /// <summary>The property <paramref name="name"/> of an object; null for anything else.</summary>
    public static JsonElement? Get(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var found)
            ? found
            : null;

    /// <summary>A non-empty string property; null also when its bytes are not valid UTF-8,
    /// which the parser lets through and only the read rejects.</summary>
    public static string? String(JsonElement value, string name)
    {
        if (Get(value, name) is not { ValueKind: JsonValueKind.String } s)
        {
            return null;
        }
        try
        {
            return s.GetString() is { Length: > 0 } text ? text : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>An integer counter: absent or not a number is zero, a whole-number float
    /// (<c>10.0</c>, which some providers send) counts, and a huge one is held at the range.</summary>
    public static long Int(JsonElement value, string name)
    {
        if (Get(value, name) is not { ValueKind: JsonValueKind.Number } n)
        {
            return 0;
        }
        if (n.TryGetInt64(out var whole))
        {
            return whole;
        }
        if (!n.TryGetDouble(out var real))
        {
            return 0;
        }
        return real >= long.MaxValue ? long.MaxValue
            : real <= long.MinValue ? long.MinValue
            : (long)real;
    }
}
