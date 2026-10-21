namespace Rtok.Mcp.Install;

/// <summary>Everything the host table depends on, so a test can build it for a fake home on any OS.</summary>
/// <param name="Home">The user's home directory (<c>HOME</c>, else <c>USERPROFILE</c>).</param>
/// <param name="Os">Whose app-data layout to follow.</param>
/// <param name="AppData"><c>%APPDATA%</c> on Windows; null elsewhere or when unset.</param>
/// <param name="Command">
/// How an entry names rtok: <c>rtok</c> when it is on <c>PATH</c>, else the absolute binary.
/// </param>
/// <param name="Exe">The running rtok binary, for the desktop apps that start without a shell <c>PATH</c>.</param>
/// <param name="Paths">
/// Config files the user set (<c>[setup.&lt;host&gt;]</c>), keyed <c>&lt;host&gt;.&lt;surface&gt;</c>,
/// already expanded; they replace the table's default for that surface.
/// </param>
public sealed record McpHostEnv(
    string Home,
    McpOs Os,
    string? AppData = null,
    string Command = "rtok",
    string? Exe = null,
    IReadOnlyDictionary<string, string>? Paths = null
);
