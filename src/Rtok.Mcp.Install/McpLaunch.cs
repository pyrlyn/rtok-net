namespace Rtok.Mcp.Install;

/// <summary>How a host starts the server when that is not the bare <c>rtok mcp</c>.</summary>
/// <param name="Command">
/// The binary to run instead of <see cref="McpServer.Command"/>: a desktop app starts without
/// a shell <c>PATH</c>, so it needs the absolute path.
/// </param>
/// <param name="HostArg">Append <c>--host &lt;host&gt;</c>, so the server knows who runs it.</param>
public sealed record McpLaunch(string? Command = null, bool HostArg = true)
{
    /// <summary><c>rtok mcp --host &lt;host&gt;</c> from <c>PATH</c>.</summary>
    public static McpLaunch Default { get; } = new();
}
