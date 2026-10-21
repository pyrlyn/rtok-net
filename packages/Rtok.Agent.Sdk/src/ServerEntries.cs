using System.Text.Json.Nodes;

namespace Rtok.Agent.Sdk;

/// <summary>
/// rtok's entry in a host's JSON server map: registered idempotently, removed only after
/// <see cref="Ownership.Judge"/> says it is rtok's.
/// </summary>
public static class ServerEntries
{
    /// <summary>
    /// Register a stdio MCP server under <c>mcpServers.&lt;name&gt;</c>, the shape Claude Code
    /// (<c>~/.claude.json</c>) and Cursor (<c>~/.cursor/mcp.json</c>) read.
    /// </summary>
    public static string RegisterMcp(
        Apply apply,
        string path,
        string name,
        string command,
        IReadOnlyList<string> args
    )
    {
        ArgumentNullException.ThrowIfNull(args);
        return Register(
            apply,
            path,
            "mcpServers",
            name,
            McpEntry(command, args),
            $"{command} {string.Join(' ', args)}"
        );
    }

    /// <summary>The <c>mcpServers.&lt;name&gt;</c> entry <see cref="RegisterMcp"/> writes.</summary>
    public static JsonObject McpEntry(string command, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new()
        {
            ["type"] = "stdio",
            ["command"] = command,
            ["args"] = new JsonArray([.. args.Select(a => (JsonNode?)a)]),
        };
    }

    /// <summary>
    /// Set <c>&lt;key&gt;.&lt;name&gt;</c> to <paramref name="entry"/> (a dotted
    /// <paramref name="key"/> walks nested objects) and report <c>&lt;key&gt;.&lt;name&gt;:
    /// &lt;summary&gt;</c>, or <see cref="Apply.NoChanges"/> when an equal entry is already there
    /// (a missing <c>"type": "stdio"</c> counts as equal).
    /// </summary>
    public static string Register(
        Apply apply,
        string path,
        string key,
        string name,
        JsonNode entry,
        string summary
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(entry);
        return HostFile.EditJson(
            apply,
            path,
            root =>
            {
                var servers = key.Split('.').Aggregate(root, HostFile.ObjectAt);
                if (
                    servers.TryGetPropertyValue(name, out var have)
                    && JsonNode.DeepEquals(
                        Ownership.WithoutDefaultType(have),
                        Ownership.WithoutDefaultType(entry)
                    )
                )
                {
                    return Apply.NoChanges;
                }
                servers[name] = entry.DeepClone();
                return $"{key}.{name}: {summary}";
            }
        );
    }

    /// <summary>
    /// Remove <c>&lt;key&gt;.&lt;name&gt;</c> when it is rtok's (<see cref="Ownership.Judge"/>
    /// against <paramref name="ours"/>, the entry the installer writes now). Foreign entries stay,
    /// and a map that ends up empty goes too, so the file reads as before rtok arrived.
    /// </summary>
    public static string Unregister(
        Apply apply,
        string path,
        string key,
        string name,
        JsonNode ours,
        Func<string, bool> isBin
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        JsonNode? node = HostFile.ReadJson(path);
        foreach (var k in key.Split('.'))
        {
            node = node is JsonObject o ? o[k] : null;
        }
        if (node is not JsonObject found || !found.TryGetPropertyValue(name, out var have))
        {
            return Apply.NoChanges;
        }
        var leave = Ownership.Judge(apply, $"{key}.{name} in {path}", have, ours, isBin);
        return leave ?? Remove(apply, path, key, name);
    }

    /// <summary>Drop the entry by name alone. Private: every caller goes through the ownership check.</summary>
    private static string Remove(Apply apply, string path, string key, string name) =>
        HostFile.EditJson(
            apply,
            path,
            root =>
            {
                var parts = key.Split('.');
                JsonObject? parent = root;
                foreach (var k in parts[..^1])
                {
                    parent = parent?[k] as JsonObject;
                }
                if (parent?[parts[^1]] is not JsonObject servers || !servers.Remove(name))
                {
                    return Apply.NoChanges;
                }
                if (servers.Count == 0)
                {
                    parent.Remove(parts[^1]);
                }
                return $"- {key}.{name}";
            }
        );
}
