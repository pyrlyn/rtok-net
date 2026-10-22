using System.Text.Json;
using Rtok.Store;

namespace Rtok.Plugins.Docs;

/// <summary>rustdoc JSON (docs.rs <c>json.zst</c>): one item per documented entry of <c>paths</c>.</summary>
public static class RustdocIndex
{
    /// <summary>
    /// The format version and the items of a rustdoc JSON document, as rtok read them: each
    /// <c>paths</c> entry's path (its <c>path</c> segments joined by <c>::</c>, else its id),
    /// kind (a string, else an object's first key, else <c>item</c>) and <c>index[id].docs</c>.
    /// Entries without docs are skipped.
    /// </summary>
    public static (long FormatVersion, List<DocEntry> Items) Read(JsonElement root)
    {
        var items = new List<DocEntry>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (0, items);
        }
        var format =
            root.TryGetProperty("format_version", out var f)
            && f.ValueKind == JsonValueKind.Number
            && f.TryGetInt64(out var v)
                ? v
                : 0;
        if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return (format, items);
        }
        var hasIndex =
            root.TryGetProperty("index", out var index) && index.ValueKind == JsonValueKind.Object;
        foreach (var entry in paths.EnumerateObject())
        {
            if (
                !hasIndex
                || !index.TryGetProperty(entry.Name, out var item)
                || item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("docs", out var docs)
                || docs.ValueKind != JsonValueKind.String
                || docs.GetString() is not { Length: > 0 } text
            )
            {
                continue;
            }
            items.Add(new DocEntry(PathOf(entry.Name, entry.Value), KindOf(entry.Value), text));
        }
        return (format, items);
    }

    private static string PathOf(string id, JsonElement summary)
    {
        if (
            summary.ValueKind != JsonValueKind.Object
            || !summary.TryGetProperty("path", out var path)
            || path.ValueKind != JsonValueKind.Array
        )
        {
            return id;
        }
        var joined = string.Join(
            "::",
            path.EnumerateArray()
                .Where(s => s.ValueKind == JsonValueKind.String)
                .Select(s => s.GetString())
        );
        return joined.Length == 0 ? id : joined;
    }

    private static string KindOf(JsonElement summary)
    {
        if (
            summary.ValueKind == JsonValueKind.Object
            && summary.TryGetProperty("kind", out var kind)
        )
        {
            if (kind.ValueKind == JsonValueKind.String)
            {
                return kind.GetString()!;
            }
            if (kind.ValueKind == JsonValueKind.Object)
            {
                foreach (var first in kind.EnumerateObject())
                {
                    return first.Name;
                }
            }
        }
        return "item";
    }
}
