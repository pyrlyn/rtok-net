using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>One MCP call of a graph tool: arguments to <see cref="GraphTools"/>, on the
/// dispatch's working directory, and the <c>index &lt;old&gt; head &lt;new&gt;</c> line in
/// front once HEAD has moved past the index (<c>outline</c> stays bare).</summary>
internal static class GraphCall
{
    public static string? Run(Ctx cx, string name, JsonElement args)
    {
        ArgumentNullException.ThrowIfNull(cx);
        var root = cx.Host.Cwd ?? Environment.CurrentDirectory;
        var filter = new GraphFilter(Str(args, "path"), Str(args, "kind"), Bool(args, "all"));
        string text;
        switch (name)
        {
            case "symbol":
                text = Str(args, "id") is { Length: > 0 } id
                    ? GraphTools.SymbolById(cx, root, id)
                    : GraphTools.Symbols(cx, root, Names(args), filter);
                break;
            case "callers":
                text = GraphTools.Callers(cx, root, Required(args, "name"), filter);
                break;
            case "impact":
                text = GraphTools.Impact(
                    cx,
                    root,
                    Required(args, "name"),
                    Int(args, "depth") ?? 2,
                    filter,
                    Str(args, "to")
                );
                break;
            case "outline":
                return GraphTools.Outline(cx, root, Required(args, "path"), filter.Kind);
            case "explore":
                text = GraphTools.Explore(cx, root, Required(args, "query"), filter);
                break;
            default:
                return null;
        }

        return IndexedHead.StalePrefix(cx, GraphRoot.Check(root)) + text;
    }

    // `names` when it lists any, else `name`; neither is an error.
    private static List<string> Names(JsonElement args)
    {
        var names = new List<string>();
        if (
            args.ValueKind is JsonValueKind.Object
            && args.TryGetProperty("names", out var list)
            && list.ValueKind is JsonValueKind.Array
        )
        {
            names.AddRange(
                list.EnumerateArray()
                    .Where(v => v.ValueKind is JsonValueKind.String)
                    .Select(v => v.GetString() ?? "")
                    .Where(s => s.Length > 0)
            );
        }

        if (names.Count == 0 && Str(args, "name") is { Length: > 0 } one)
        {
            names.Add(one);
        }

        return names.Count > 0
            ? names
            : throw new ArgumentException("missing `name` or `id`", nameof(args));
    }

    private static string Required(JsonElement args, string key) =>
        Str(args, key) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"missing `{key}`", nameof(args));

    private static string Str(JsonElement args, string key) =>
        args.ValueKind is JsonValueKind.Object
        && args.TryGetProperty(key, out var v)
        && v.ValueKind is JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static bool Bool(JsonElement args, string key) =>
        args.ValueKind is JsonValueKind.Object
        && args.TryGetProperty(key, out var v)
        && v.ValueKind is JsonValueKind.True;

    private static int? Int(JsonElement args, string key) =>
        args.ValueKind is JsonValueKind.Object
        && args.TryGetProperty(key, out var v)
        && v.ValueKind is JsonValueKind.Number
        && v.TryGetInt32(out var n)
            ? n
            : null;
}
