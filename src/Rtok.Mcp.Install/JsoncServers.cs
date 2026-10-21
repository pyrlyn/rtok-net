using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Jsonc;

namespace Rtok.Mcp.Install;

/// <summary>JSONC: edited in place through Pyrlyn.Jsonc, so comments and layout stay.</summary>
internal static class JsoncServers
{
    public static JsonObject? Servers(McpSpec spec, string text)
    {
        var doc = Parse(spec, text);
        if (doc.IsEmpty)
        {
            return null;
        }
        var table = doc.Get(Path(spec.KeyPath));
        return table is { ValueKind: JsonValueKind.Object } t ? JsonObject.Create(t) : null;
    }

    public static string With(McpSpec spec, string text, JsonObject entry)
    {
        var doc = Parse(spec, text);
        try
        {
            return doc.Set(Path([.. spec.KeyPath, spec.Server.Name]), entry).Text;
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidDataException(
                $"{spec.Path}: {e.Message}; rtok leaves the file as it is",
                e
            );
        }
    }

    /// <summary>Removes the entry, then every table on the way it alone kept alive, unless that table holds a comment.</summary>
    public static string Without(McpSpec spec, string text)
    {
        var doc = Parse(spec, text);
        JsoncSegment[] path = Path([.. spec.KeyPath, spec.Server.Name]);
        if (!doc.Contains(path))
        {
            return text;
        }
        doc = doc.Remove(path);
        for (var depth = spec.KeyPath.Count; depth > 0; depth--)
        {
            var raw = doc.GetRaw(path.AsSpan(0, depth));
            if (raw is null || !IsBareEmptyObject(raw))
            {
                break;
            }
            doc = doc.Remove(path.AsSpan(0, depth));
        }
        return doc.Text;
    }

    private static bool IsBareEmptyObject(string raw) =>
        string.Equals(
            string.Concat(raw.Where(c => !char.IsWhiteSpace(c))),
            "{}",
            StringComparison.Ordinal
        );

    private static JsoncDocument Parse(McpSpec spec, string text)
    {
        try
        {
            return JsoncDocument.Parse(text, spec.Path);
        }
        catch (JsoncException e)
        {
            throw new InvalidDataException(e.Message, e);
        }
    }

    private static JsoncSegment[] Path(IEnumerable<string> keys) =>
        [.. keys.Select(JsoncSegment.Of)];
}
