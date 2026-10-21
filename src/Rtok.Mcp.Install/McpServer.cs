namespace Rtok.Mcp.Install;

/// <summary>A stdio MCP server rtok serves: the one list every <see cref="McpSpec"/> entry is built from.</summary>
/// <param name="Name">The key the entry has in the host's servers table.</param>
/// <param name="Command">The binary, as found on <c>PATH</c>.</param>
/// <param name="Args">Its arguments.</param>
/// <param name="Env">Environment variables the entry sets; usually none.</param>
public sealed record McpServer(
    string Name,
    string Command,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env
)
{
    /// <summary>rtok's own server: <c>rtok mcp</c>.</summary>
    public static McpServer Rtok { get; } =
        new("rtok", "rtok", ["mcp"], new Dictionary<string, string>(StringComparer.Ordinal));
}
