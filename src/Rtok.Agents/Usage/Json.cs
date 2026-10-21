using System.Globalization;
using System.Text.Json;

namespace Rtok.Agents.Usage;

/// <summary>The few ways the readers look into a log's JSON, each forgiving of a missing or odd value.</summary>
internal static class Json
{
    /// <summary>The document of one line, or null when it is not JSON.</summary>
    public static JsonElement? Parse(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The value at <paramref name="path"/>, one property name per step.</summary>
    public static JsonElement? At(JsonElement value, params ReadOnlySpan<string> path)
    {
        var at = value;
        foreach (var name in path)
        {
            if (at.ValueKind != JsonValueKind.Object || !at.TryGetProperty(name, out at))
            {
                return null;
            }
        }
        return at;
    }

    /// <summary>The object at <paramref name="path"/>, or null when it is absent or not an object.</summary>
    public static JsonElement? Object(JsonElement value, params ReadOnlySpan<string> path) =>
        At(value, path) is { ValueKind: JsonValueKind.Object } o ? o : null;

    /// <summary>The string at <paramref name="path"/>, or null when it is absent or not a string.</summary>
    public static string? Text(JsonElement value, params ReadOnlySpan<string> path) =>
        At(value, path) is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;

    /// <summary>The integer at <paramref name="path"/>, or null when it is absent or not an integer.</summary>
    public static long? Integer(JsonElement value, params ReadOnlySpan<string> path) =>
        At(value, path) is { ValueKind: JsonValueKind.Number } n && n.TryGetInt64(out var i)
            ? i
            : null;

    /// <summary>
    /// A token counter: the integer at <paramref name="path"/>, negatives and absence as 0, capped
    /// at <see cref="MaxCount"/> so a reader's sums of a few counters cannot overflow.
    /// </summary>
    public static long Count(JsonElement value, params ReadOnlySpan<string> path) =>
        Math.Clamp(Integer(value, path) ?? 0, 0, MaxCount);

    /// <summary>JavaScript's largest exact integer: no host's log holds a real count above it.</summary>
    public const long MaxCount = (1L << 53) - 1;

    /// <summary>The <c>timestamp</c> of a line in unix seconds; 0 when it has none or no zone.</summary>
    public static long Timestamp(JsonElement value) =>
        Text(value, "timestamp") is { } stamp && TryInstant(stamp, out var at)
            ? at.ToUnixTimeSeconds()
            : 0;

    /// <summary>The lines of a file, as <c>str::lines</c> splits them: on <c>\n</c>, a trailing <c>\r</c> dropped.</summary>
    public static IEnumerable<string> Lines(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            var stop = end < 0 ? text.Length : end;
            var line = text[start..stop];
            yield return line.EndsWith('\r') ? line[..^1] : line;
            start = stop + 1;
        }
    }

    /// <summary>An RFC 3339 instant: a date, a time and a zone (<c>Z</c> or an offset).</summary>
    private static bool TryInstant(string text, out DateTimeOffset at)
    {
        at = default;
        var t = text.IndexOfAny(['T', 't', ' ']);
        if (t < 0 || !HasZone(text.AsSpan(t)))
        {
            return false;
        }
        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out at
        );
    }

    private static bool HasZone(ReadOnlySpan<char> time) =>
        time.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
        || time.LastIndexOfAny('+', '-') is > 0 and var sign && sign >= time.Length - 6;
}
