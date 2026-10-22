using System.Text.Json.Nodes;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>One MCP server entry in one host file.</summary>
/// <param name="Set">The host surface whose servers it joins: <c>claude:cli</c>.</param>
/// <param name="Agent">The host.</param>
/// <param name="Source">The file.</param>
/// <param name="KeyPath">The servers table's key path in the file.</param>
/// <param name="Name">The name the host knows it by (<c>plugin:id:name</c> for a plugin's).</param>
/// <param name="Key">Its own key in the table.</param>
/// <param name="Scope">Where it comes from.</param>
/// <param name="Format">The file's syntax.</param>
/// <param name="Node">The entry.</param>
/// <param name="Spec">The host surface the entry belongs to, which the TOML editor cuts it by.</param>
internal sealed record McpEntry(
    string Set,
    string Agent,
    string Source,
    IReadOnlyList<string> KeyPath,
    string Name,
    string Key,
    Scope Scope,
    McpFormat Format,
    JsonObject Node,
    McpSpec Spec
)
{
    /// <summary>The entry's place as reports print it: <c>mcpServers.x</c>.</summary>
    public string Path => string.Join('.', [.. KeyPath, Key]);

    /// <summary>Which copy of one name the host uses: local over project over user over plugin.</summary>
    public int Wins =>
        Scope switch
        {
            Scope.Local => 3,
            Scope.Project => 2,
            Scope.User => 1,
            _ => 0,
        };
}
