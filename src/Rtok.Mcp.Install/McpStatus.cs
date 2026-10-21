using System.Text.Json.Nodes;

namespace Rtok.Mcp.Install;

/// <summary>Reads a surface's MCP status straight from its config file (rtok T278); never writes.</summary>
public static class McpStatus
{
    /// <summary>
    /// The row for <paramref name="spec"/>: present when the entry runs <c>rtok mcp</c> for this
    /// host (any rtok binary, with or without <c>--host</c>), stale with what it runs otherwise,
    /// missing when there is none. A file that cannot be read or parsed is missing, with why.
    /// </summary>
    /// <param name="spec">The surface.</param>
    /// <param name="disk">Where the file is read.</param>
    /// <param name="isBin">Whether a string names the rtok binary.</param>
    /// <param name="home">The home directory, spelled <c>~</c> in the row's file; null to keep the path.</param>
    /// <param name="plugin">What else serves rtok's server on this surface, if anything.</param>
    public static McpRow Of(
        McpSpec spec,
        McpDisk disk,
        Func<string, bool> isBin,
        string? home = null,
        string? plugin = null
    )
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(isBin);
        var file = Tilde(spec.Path, home);
        // Status never writes, so a strict-JSON file is read as JSONC: a comment is no reason to report "missing".
        var read = spec.Format == McpFormat.Json ? spec with { Format = McpFormat.Jsonc } : spec;
        JsonNode? have;
        try
        {
            have = McpConfig.Entry(read, disk.Read(spec.Path));
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            return new McpRow(spec.Surface, file, McpEntryState.Missing, e.Message, plugin);
        }
        if (have is null)
        {
            return new McpRow(spec.Surface, file, McpEntryState.Missing, null, plugin);
        }
        var diff = StaleDiff(spec, have, isBin);
        var state = diff is null ? McpEntryState.Present : McpEntryState.Stale;
        return new McpRow(spec.Surface, file, state, diff, plugin);
    }

    /// <summary>
    /// Null when <paramref name="have"/> runs <c>rtok mcp</c> or <c>rtok mcp --host &lt;host&gt;</c>
    /// through any rtok binary, as <c>command</c> plus <c>args</c> or one <c>command</c> array;
    /// otherwise <c>runs `…`, want `…`</c>.
    /// </summary>
    internal static string? StaleDiff(McpSpec spec, JsonNode have, Func<string, bool> isBin)
    {
        var argv = have["command"] switch
        {
            JsonValue c when c.TryGetValue<string>(out var bin) => [bin, .. Strings(have["args"])],
            var c => Strings(c),
        };
        List<string> bare = [.. spec.Server.Args];
        List<string> hosted = [.. bare, "--host", spec.Host];
        var ours =
            argv.Count > 0
            && isBin(argv[0])
            && (
                argv.Skip(1).SequenceEqual(bare, StringComparer.Ordinal)
                || argv.Skip(1).SequenceEqual(hosted, StringComparer.Ordinal)
            );
        var want = string.Join(' ', [spec.Server.Command, .. hosted]);
        return ours ? null : $"runs `{string.Join(' ', argv)}`, want `{want}`";
    }

    private static List<string> Strings(JsonNode? node) =>
        node is JsonArray a
            ?
            [
                .. a.OfType<JsonValue>()
                    .Select(v => v.TryGetValue<string>(out var s) ? s : null)
                    .OfType<string>(),
            ]
            : [];

    /// <summary><paramref name="path"/> with <paramref name="home"/> spelled <c>~</c>.</summary>
    internal static string Tilde(string path, string? home)
    {
        if (string.IsNullOrEmpty(home))
        {
            return path;
        }
        var root = home.TrimEnd('/', '\\');
        return
            path.Length > root.Length
            && path.StartsWith(root, StringComparison.Ordinal)
            && path[root.Length] is '/' or '\\'
            ? "~" + path[root.Length..]
            : path;
    }
}
