using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>
/// Every MCP server entry of every host surface rtok knows: per surface, Gemini's extension
/// manifest, Claude's project <c>.mcp.json</c>, the <c>~/.claude.json</c> project scope and the
/// enabled plugins' <c>.mcp.json</c>, then the surface's own file. A file that does not parse is
/// skipped.
/// </summary>
internal static class McpScan
{
    public static List<McpEntry> Entries(
        DoctorHome home,
        IReadOnlyList<(string Id, string Dir)> plugins
    )
    {
        var entries = new List<McpEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in McpHosts.All(home.Hosts.McpEnv, home.Fs))
        {
            var cli =
                string.Equals(spec.Host, "claude", StringComparison.Ordinal)
                && string.Equals(spec.Surface, "cli", StringComparison.Ordinal);
            var path = cli ? home.PathOf(home.Config.Doctor.ClaudeJson) : spec.Path;
            if (!seen.Add(path + "\0" + string.Join('.', spec.KeyPath)))
            {
                continue;
            }
            var set = $"{spec.Host}:{spec.Surface}";
            void Add(
                string file,
                IReadOnlyList<string> keys,
                Scope scope,
                McpFormat format,
                string? plugin = null,
                string? root = null
            ) => entries.AddRange(Read(home, spec, set, file, keys, scope, format, plugin, root));
            if (string.Equals(spec.Host, "gemini", StringComparison.Ordinal))
            {
                Add(
                    home.InHome(".gemini/extensions/rtok/gemini-extension.json"),
                    spec.KeyPath,
                    Scope.Plugin,
                    McpFormat.Jsonc
                );
            }
            if (cli)
            {
                Add(
                    home.Under(home.Cwd, ".mcp.json"),
                    ["mcpServers"],
                    Scope.Project,
                    McpFormat.Jsonc
                );
                Add(path, ["projects", home.Cwd, "mcpServers"], Scope.Local, McpFormat.Jsonc);
                foreach (var (id, dir) in plugins)
                {
                    Add(
                        home.Under(dir, ".mcp.json"),
                        ["mcpServers"],
                        Scope.Plugin,
                        McpFormat.Jsonc,
                        id,
                        dir
                    );
                }
            }
            Add(
                path,
                spec.KeyPath,
                Scope.User,
                spec.Format == McpFormat.Toml ? McpFormat.Toml : McpFormat.Jsonc
            );
        }
        return entries;
    }

    private static IEnumerable<McpEntry> Read(
        DoctorHome home,
        McpSpec spec,
        string set,
        string file,
        IReadOnlyList<string> keys,
        Scope scope,
        McpFormat format,
        string? plugin,
        string? root
    )
    {
        if (Servers(home, file, keys, format) is not { } servers)
        {
            yield break;
        }
        foreach (var (key, node) in servers)
        {
            if (node is not JsonObject entry)
            {
                continue;
            }
            if (plugin is null)
            {
                yield return new McpEntry(
                    set,
                    spec.Host,
                    file,
                    keys,
                    key,
                    key,
                    scope,
                    format,
                    entry,
                    spec
                );
                continue;
            }
            var at = plugin.IndexOf('@', StringComparison.Ordinal);
            var name = $"plugin:{(at < 0 ? plugin : plugin[..at])}:{key}";
            yield return new McpEntry(
                set,
                spec.Host,
                file,
                keys,
                name,
                key,
                scope,
                format,
                (JsonObject)PluginRoot(entry, root!)!,
                spec
            );
        }
    }

    /// <summary>The servers table; null when the file is missing, does not parse or has no table.</summary>
    internal static JsonObject? Servers(
        DoctorHome home,
        string file,
        IReadOnlyList<string> keys,
        McpFormat format
    ) => home.TryRead(file) is { } text ? Servers(text, file, keys, format) : null;

    /// <summary>The servers table in <paramref name="text"/>; null when it does not parse or has none.</summary>
    internal static JsonObject? Servers(
        string text,
        string file,
        IReadOnlyList<string> keys,
        McpFormat format
    )
    {
        JsonNode? node;
        try
        {
            node =
                format == McpFormat.Toml
                    ? DoctorHome.ParseToml(text, file)
                    : DoctorHome.ParseJsonc(text);
        }
        catch (Exception e) when (e is JsonException or Pyrlyn.Config.Layered.ConfigFileException)
        {
            return null;
        }
        foreach (var key in keys)
        {
            node = node is JsonObject o ? o[key] : null;
        }
        return node as JsonObject;
    }

    /// <summary>A copy with <c>${CLAUDE_PLUGIN_ROOT}</c> replaced in every string.</summary>
    private static JsonNode? PluginRoot(JsonNode? node, string root) =>
        node switch
        {
            JsonObject o => new JsonObject(
                o.Select(kv => KeyValuePair.Create(kv.Key, PluginRoot(kv.Value, root)))
            ),
            JsonArray a => new JsonArray([.. a.Select(x => PluginRoot(x, root))]),
            JsonValue v when DoctorHome.Str(v) is { } s => JsonValue.Create(
                s.Replace("${CLAUDE_PLUGIN_ROOT}", root, StringComparison.Ordinal)
            ),
            _ => node?.DeepClone(),
        };
}
