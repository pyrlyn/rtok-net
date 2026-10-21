using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;

namespace Rtok.Mcp.Install;

/// <summary>Strict JSON: parsed, edited as a tree and written back pretty (rtok T79).</summary>
internal static class JsonServers
{
    public static JsonObject? Servers(McpSpec spec, string text)
    {
        var node = Root(spec, text);
        foreach (var key in spec.KeyPath)
        {
            node = node is JsonObject o ? o[key] : null;
        }
        return node as JsonObject;
    }

    public static string With(McpSpec spec, string text, JsonObject entry)
    {
        var root = Root(spec, text);
        var table = root as JsonObject ?? throw NotObject(spec, "the root");
        for (var i = 0; i < spec.KeyPath.Count; i++)
        {
            var key = spec.KeyPath[i];
            if (table[key] is null && !table.ContainsKey(key))
            {
                table[key] = new JsonObject();
            }
            table =
                table[key] as JsonObject
                ?? throw NotObject(spec, string.Join('.', spec.KeyPath.Take(i + 1)));
        }
        table[spec.Server.Name] = entry.DeepClone();
        return HostFile.ToJson(root!);
    }

    /// <summary>Removes the entry, then every table on the way that only it kept alive.</summary>
    public static string Without(McpSpec spec, string text)
    {
        var root = Root(spec, text);
        List<JsonObject> chain = [root as JsonObject ?? throw NotObject(spec, "the root")];
        foreach (var key in spec.KeyPath)
        {
            if (chain[^1][key] is not JsonObject next)
            {
                return text;
            }
            chain.Add(next);
        }
        chain[^1].Remove(spec.Server.Name);
        for (var i = chain.Count - 1; i > 0 && chain[i].Count == 0; i--)
        {
            chain[i - 1].Remove(spec.KeyPath[i - 1]);
        }
        return HostFile.ToJson(root!);
    }

    /// <summary>The parsed text; a blank file is an empty object, a literal <c>null</c> stays null (not an object).</summary>
    private static JsonNode? Root(McpSpec spec, string text) =>
        text.AsSpan().Trim().SequenceEqual("null")
            ? null
            : HostFile.ParseStrictJson(spec.Path, text);

    private static InvalidDataException NotObject(McpSpec spec, string what) =>
        new($"{spec.Path}: {what} is not an object; rtok leaves the file as it is");
}
