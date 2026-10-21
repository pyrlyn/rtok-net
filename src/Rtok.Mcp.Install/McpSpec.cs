using System.Text.Json.Nodes;

namespace Rtok.Mcp.Install;

/// <summary>
/// One host surface's MCP config: where rtok's entry lives, how the file is shaped, and the
/// entry rtok writes there. Hosts differ only in this data; <see cref="McpInstaller"/> and
/// <see cref="McpStatus"/> are the one core for all of them (rtok T277).
/// </summary>
/// <param name="Host">The host id, as in <c>rtok agents install &lt;host&gt;</c>.</param>
/// <param name="Surface">The row label in <c>agents info</c>: <c>cli</c>, <c>desktop</c>, <c>stable</c>, …</param>
/// <param name="Client">Which client of the host reads the file.</param>
/// <param name="Path">The config file, with the home directory expanded.</param>
/// <param name="Format">The file's syntax.</param>
/// <param name="KeyPath">
/// The path to the servers table: <c>["mcpServers"]</c>, <c>["mcp", "servers"]</c>, or Amp's
/// one flat key <c>["amp.mcpServers"]</c>.
/// </param>
/// <param name="Server">The server the entry starts.</param>
/// <param name="Launch">How the host starts it.</param>
/// <param name="Shape">How the entry is spelled.</param>
/// <param name="DuplicateName">What the host does with a same-named plugin server.</param>
/// <param name="PluginServes">The name rtok's own host plugin serves there, if any (D33: usually none).</param>
public sealed record McpSpec(
    string Host,
    string Surface,
    McpClient Client,
    string Path,
    McpFormat Format,
    IReadOnlyList<string> KeyPath,
    McpServer Server,
    McpLaunch Launch,
    EntryShape Shape,
    DuplicateName DuplicateName,
    string? PluginServes = null
)
{
    /// <summary>The entry's place as reports spell it: <c>mcpServers.rtok</c>.</summary>
    public string At => string.Join('.', [.. KeyPath, Server.Name]);

    /// <summary>The binary and its arguments as the entry spells them.</summary>
    public IReadOnlyList<string> Argv
    {
        get
        {
            List<string> argv = [Launch.Command ?? Server.Command, .. Server.Args];
            if (Launch.HostArg)
            {
                argv.AddRange(["--host", Host]);
            }
            return argv;
        }
    }

    /// <summary><c>rtok mcp --host claude</c>: what an install report prints after <see cref="At"/>.</summary>
    public string Summary() => string.Join(' ', Argv);

    /// <summary>The entry written under <see cref="KeyPath"/>; TOML files get the same keys as a table.</summary>
    public JsonObject Entry()
    {
        var argv = Argv;
        var entry = new JsonObject();
        if (Shape is EntryShape.TypedCommand)
        {
            entry["type"] = "stdio";
        }
        else if (Shape is EntryShape.LocalCommand or EntryShape.LocalArgv)
        {
            entry["type"] = "local";
        }
        if (Shape is EntryShape.LocalArgv)
        {
            entry["command"] = Strings(argv);
        }
        else
        {
            entry["command"] = argv[0];
            entry["args"] = Strings(argv.Skip(1));
        }
        if (Server.Env.Count > 0)
        {
            entry["env"] = new JsonObject(
                Server.Env.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))
            );
        }
        switch (Shape)
        {
            case EntryShape.LocalCommand:
                entry["tools"] = Strings(["*"]);
                break;
            case EntryShape.LocalArgv:
                entry["enabled"] = true;
                break;
            case EntryShape.CommandWritesApproved:
                entry["default_tools_approval_mode"] = "writes";
                break;
        }
        return entry;
    }

    private static JsonArray Strings(IEnumerable<string> items) =>
        new([.. items.Select(a => (JsonNode?)a)]);
}
