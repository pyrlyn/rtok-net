using System.Globalization;
using System.Text.Json;

namespace Rtok.Tasks.Remote;

/// <summary>Field readers for the services' answers; a missing or mistyped field is
/// <see cref="TaskException"/> that names it.</summary>
internal static class RemoteJson
{
    public static JsonElement Get(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v
            : throw Unexpected(name);

    public static JsonElement? Find(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(name, out var v)
        && v.ValueKind != JsonValueKind.Null
            ? v
            : null;

    public static string Str(JsonElement e, string name) =>
        Get(e, name) is { ValueKind: JsonValueKind.String } v
            ? v.GetString()!
            : throw Unexpected(name);

    public static string? OptStr(JsonElement e, string name) =>
        Find(e, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    public static long Long(JsonElement e, string name) =>
        Get(e, name) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt64(out var n)
            ? n
            : throw Unexpected(name);

    /// <summary>An RFC 3339 time in Unix seconds.</summary>
    public static long Unix(JsonElement e, string name) =>
        DateTimeOffset.TryParse(
            Str(e, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var at
        )
            ? at.ToUnixTimeSeconds()
            : throw Unexpected(name);

    private static TaskException Unexpected(string field) => new($"unexpected JSON: no `{field}`");

    /// <summary>Label names from an array of strings or of <c>{"name": ...}</c> objects.</summary>
    public static List<string> Labels(JsonElement e, string name)
    {
        var labels = new List<string>();
        if (Find(e, name) is not { ValueKind: JsonValueKind.Array } array)
        {
            return labels;
        }
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                labels.Add(item.GetString()!);
            }
            else if (OptStr(item, "name") is { } label)
            {
                labels.Add(label);
            }
        }
        return labels;
    }
}
