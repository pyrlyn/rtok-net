using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Pyrlyn.Jsonc;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>
/// The minimal edit of one host file: the selected hooks and MCP entries cut out through
/// Pyrlyn.Jsonc or the TOML editor, every other byte kept, and the result checked against the
/// same removal on the parsed tree.
/// </summary>
internal static partial class FixEdit
{
    /// <summary>The file's new text.</summary>
    /// <exception cref="InvalidDataException">An entry is not there, or the edit would change more than it.</exception>
    public static string Apply(
        string path,
        string text,
        IReadOnlyList<Problem> hooks,
        IReadOnlyList<McpEntry> mcp
    )
    {
        var toml = mcp.Any(e => e.Format == McpFormat.Toml);
        var before = Parse(text, path, toml);
        var expected = before?.DeepClone();
        string after;
        if (toml)
        {
            after = text;
            foreach (var e in mcp)
            {
                if (
                    McpScan.Servers(after, path, e.KeyPath, McpFormat.Toml)?.ContainsKey(e.Key)
                    != true
                )
                {
                    throw new InvalidDataException("an entry could not be located");
                }
                var spec = e.Spec with
                {
                    Path = path,
                    KeyPath = e.KeyPath,
                    Format = McpFormat.Toml,
                    Server = e.Spec.Server with { Name = e.Key },
                };
                after = McpConfig.Without(spec, after);
                Cut(expected, [.. e.KeyPath, e.Key]);
            }
        }
        else
        {
            var doc = JsoncDocument.Parse(text, path);
            foreach (var (ev, group, hook) in Hooks(hooks))
            {
                doc = RemoveHook(doc, expected, ev, group, hook);
            }
            foreach (var e in mcp)
            {
                JsoncSegment[] at = [.. e.KeyPath.Select(JsoncSegment.Of), JsoncSegment.Of(e.Key)];
                if (!doc.Contains(at))
                {
                    throw new InvalidDataException("an entry could not be located");
                }
                doc = doc.Remove(at);
                Cut(expected, [.. e.KeyPath, e.Key]);
            }
            after = doc.Text;
        }
        var actual = Parse(after, path, toml);
        if (
            !JsonNode.DeepEquals(actual, expected)
            && !JsonNode.DeepEquals(actual, Pruned(expected, mcp))
        )
        {
            throw new InvalidDataException(
                hooks.Count > 0
                    ? "the edit would change more than the selected hooks"
                    : "the edit would change more than the duplicate entries"
            );
        }
        return after;
    }

    /// <summary>The hooks to cut as (event, group, index in group), last first so earlier indices stay valid.</summary>
    private static IEnumerable<(string Event, int Group, int? Hook)> Hooks(
        IReadOnlyList<Problem> hooks
    ) =>
        hooks
            .Select(p => HookPath().Match(p.Path))
            .Where(m => m.Success)
            .Select(m =>
                (
                    m.Groups["e"].Value,
                    int.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture),
                    m.Groups["h"].Success
                        ? int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture)
                        : (int?)null
                )
            )
            .Distinct()
            .OrderBy(x => x.Item1, StringComparer.Ordinal)
            .ThenByDescending(x => x.Item2)
            .ThenByDescending(x => x.Item3 ?? -1);

    /// <summary>Cuts one hook; a group it leaves empty goes with it, and an event left without groups too.</summary>
    private static JsoncDocument RemoveHook(
        JsoncDocument doc,
        JsonNode? expected,
        string ev,
        int group,
        int? hook
    )
    {
        JsoncSegment[] at = hook is { } h ? ["hooks", ev, group, "hooks", h] : ["hooks", ev, group];
        if (!doc.Contains(at))
        {
            throw new InvalidDataException("an entry could not be located");
        }
        doc = doc.Remove(at);
        Cut(expected, at);
        if (hook is not null && Empty(doc.Get("hooks", ev, group, "hooks")))
        {
            doc = doc.Remove("hooks", ev, group);
            Cut(expected, ["hooks", ev, group]);
        }
        if (Empty(doc.Get("hooks", ev)))
        {
            doc = doc.Remove("hooks", ev);
            Cut(expected, ["hooks", ev]);
        }
        return doc;
    }

    private static bool Empty(JsonElement? e) =>
        e is { ValueKind: JsonValueKind.Array } a && a.GetArrayLength() == 0;

    /// <summary>The same removal on the parsed tree.</summary>
    private static void Cut(JsonNode? root, JsoncSegment[] path)
    {
        var parent = root;
        for (var i = 0; i < path.Length - 1 && parent is not null; i++)
        {
            parent = Child(parent, path[i]);
        }
        switch (parent, path[^1])
        {
            case (JsonArray a, { IsIndex: true } s) when s.Index < a.Count:
                a.RemoveAt(s.Index);
                break;
            case (JsonObject o, { IsIndex: false } s):
                o.Remove(s.Key!);
                break;
        }
    }

    private static void Cut(JsonNode? root, IReadOnlyList<string> path) =>
        Cut(root, [.. path.Select(JsoncSegment.Of)]);

    private static JsonNode? Child(JsonNode node, JsoncSegment s) =>
        (node, s) switch
        {
            (JsonArray a, { IsIndex: true }) when s.Index < a.Count => a[s.Index],
            (JsonObject o, { IsIndex: false }) => o[s.Key!],
            _ => null,
        };

    /// <summary>The expected tree with tables the cut MCP entries alone kept alive removed, as the TOML editor does.</summary>
    private static JsonNode? Pruned(JsonNode? expected, IReadOnlyList<McpEntry> mcp)
    {
        var tree = expected?.DeepClone();
        foreach (var e in mcp)
        {
            for (var depth = e.KeyPath.Count; depth > 0; depth--)
            {
                var node = tree;
                foreach (var key in e.KeyPath.Take(depth))
                {
                    node = (node as JsonObject)?[key];
                }
                if (node is JsonObject { Count: 0 })
                {
                    Cut(tree, [.. e.KeyPath.Take(depth)]);
                }
            }
        }
        return tree;
    }

    private static JsonNode? Parse(string text, string path, bool toml)
    {
        try
        {
            return toml ? DoctorHome.ParseToml(text, path) : DoctorHome.ParseJsonc(text);
        }
        catch (Exception e) when (e is JsonException or Pyrlyn.Config.Layered.ConfigFileException)
        {
            throw new InvalidDataException($"cannot parse {path}: {e.Message}", e);
        }
    }

    [GeneratedRegex(
        @"^hooks\.(?<e>[^\[\]]+)\[(?<g>\d+)\](?:\.hooks\[(?<h>\d+)\])?$",
        RegexOptions.None,
        1000
    )]
    private static partial Regex HookPath();
}
