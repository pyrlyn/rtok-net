using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Jsonc;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// <see cref="JsoncDocument"/> on any bytes: parsing fails only with <see cref="JsoncException"/>;
/// a set, insert or remove at a path the document has gives a document that holds the edit,
/// and an absent path changes nothing.
/// </summary>
internal static class JsoncEdit
{
    private static readonly JsonNode?[] Values =
    [
        null,
        JsonValue.Create(7),
        JsonValue.Create("fuzz \"\u00e9\" \u2028"),
        new JsonObject { ["a"] = new JsonArray(1, "b") },
        new JsonArray(),
    ];

    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }
        var pick = data[0];
        JsoncDocument doc;
        try
        {
            doc = JsoncDocument.Parse(data[1..]);
        }
        catch (JsoncException)
        {
            return;
        }
        FuzzInput.Check(doc.Utf8.Span.SequenceEqual(data[1..]), "parse changed the bytes");

        var paths = new List<JsoncSegment[]>();
        if (doc.Get() is { } root)
        {
            Collect(root, [], paths);
        }
        var value = Values[pick % Values.Length];
        if (paths.Count == 0)
        {
            Holds(doc.Set(["fuzz"], value?.DeepClone()), ["fuzz"], value);
            return;
        }
        var path = paths[pick / Values.Length % paths.Count];
        try
        {
            Edit(doc, path, pick, value);
        }
        catch (InvalidOperationException e) when (e.InnerException is not JsoncException)
        {
            // A duplicated key: the path walks into the last one, which may not be a container.
        }
        FuzzInput.Check(
            ReferenceEquals(doc.Remove("\0absent"), doc),
            "Remove of an absent key changed the document"
        );
    }

    private static void Edit(JsoncDocument doc, JsoncSegment[] path, byte pick, JsonNode? value)
    {
        switch (pick % 3)
        {
            case 0:
                Holds(doc.Set(path, value?.DeepClone()), path, value);
                break;
            case 1 when path.Length > 0:
                var removed = doc.Remove(path);
                FuzzInput.Check(
                    path[^1].IsIndex || !removed.Contains(path),
                    $"{JsoncSegment.Describe(path)} still there after Remove"
                );
                break;
            default:
                if (doc.Get(path) is { ValueKind: JsonValueKind.Object })
                {
                    JsoncSegment[] added = [.. path, "fuzz \"new\""];
                    Holds(doc.Set(added, value?.DeepClone()), added, value);
                }
                else if (doc.Get(path) is { ValueKind: JsonValueKind.Array } array)
                {
                    JsoncSegment[] added = [.. path, array.GetArrayLength()];
                    Holds(doc.Set(added, value?.DeepClone()), added, value);
                }
                break;
        }
    }

    /// <summary>The edited document re-parses to the same text and holds <paramref name="value"/>.</summary>
    private static void Holds(JsoncDocument edited, JsoncSegment[] path, JsonNode? value)
    {
        FuzzInput.Check(
            string.Equals(
                JsoncDocument.Parse(edited.Text).Text,
                edited.Text,
                StringComparison.Ordinal
            ),
            "edit does not re-parse"
        );
        using var want = JsonDocument.Parse(value?.ToJsonString() ?? "null");
        FuzzInput.Check(
            edited.Get(path) is { } got && JsonElement.DeepEquals(got, want.RootElement),
            $"{JsoncSegment.Describe(path)} does not hold the value set"
        );
    }

    /// <summary>Up to 64 paths, at most 8 deep, root first.</summary>
    private static void Collect(JsonElement node, JsoncSegment[] path, List<JsoncSegment[]> paths)
    {
        if (paths.Count >= 64 || path.Length > 8)
        {
            return;
        }
        paths.Add(path);
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in node.EnumerateObject())
            {
                Collect(member.Value, [.. path, member.Name], paths);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in node.EnumerateArray())
            {
                Collect(item, [.. path, i++], paths);
            }
        }
    }
}
