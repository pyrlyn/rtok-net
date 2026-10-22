using Rtok.Agents.Hosts;
using Rtok.Config;

namespace Rtok.Doctor;

/// <summary>
/// Lists the tool descriptions of a stdio MCP server, or null when it does not answer in time.
/// The only probe that starts a process; tests pass a fake.
/// </summary>
/// <param name="server">The server to ask.</param>
/// <param name="limit">How long it may take.</param>
public delegate IReadOnlyList<string>? McpProbe(McpCommand server, TimeSpan limit);
