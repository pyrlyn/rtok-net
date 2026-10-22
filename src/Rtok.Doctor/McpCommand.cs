using Rtok.Agents.Hosts;
using Rtok.Config;

namespace Rtok.Doctor;

/// <summary>An MCP server as a host launches it.</summary>
/// <param name="Name">The entry's name.</param>
/// <param name="Command">The program; empty for a URL server.</param>
/// <param name="Args">Its arguments.</param>
/// <param name="Env">The environment the entry sets.</param>
public sealed record McpCommand(
    string Name,
    string Command,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env
);
