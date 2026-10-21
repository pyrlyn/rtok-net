using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>One app of one host, as <c>agents list --json</c>, <c>agents info --json</c> and the web model report it.</summary>
/// <param name="Host">The host id.</param>
/// <param name="Kind"><c>cli</c> or <c>desktop</c>.</param>
/// <param name="Name">The app's name.</param>
/// <param name="Present">Whether the app is on this machine.</param>
/// <param name="App">Where it is; null when absent.</param>
/// <param name="Version">What its <c>--version</c> says; null when absent.</param>
/// <param name="Config">The files rtok edits for it.</param>
/// <param name="Modules">Each module's state; empty when absent.</param>
/// <param name="Mcp">Each MCP surface's status; empty when absent.</param>
public sealed record AgentListRow(
    string Host,
    string Kind,
    string Name,
    bool Present,
    string? App,
    string? Version,
    IReadOnlyList<string> Config,
    IReadOnlyList<ModuleRow> Modules,
    IReadOnlyList<McpRow> Mcp
);
