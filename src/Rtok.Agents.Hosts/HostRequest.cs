using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>One <c>agents install|update|uninstall</c>: which hosts, what to do, which of their apps.</summary>
/// <param name="Hosts">The host ids, in the order given.</param>
/// <param name="Mode">Install, update or remove.</param>
/// <param name="Cli">Only the CLI apps (<c>--cli</c>).</param>
/// <param name="Desktop">Only the desktop apps (<c>--desktop</c>).</param>
/// <param name="All">Every app (<c>--all</c>); also the default when neither of the others is given.</param>
/// <param name="Replace">Install on Claude Code takes out the token hooks of the tools rtok replaces instead (<c>--replace</c>); elsewhere it is a plain install.</param>
public sealed record HostRequest(
    IReadOnlyList<string> Hosts,
    McpMode Mode,
    bool Cli = false,
    bool Desktop = false,
    bool All = false,
    bool Replace = false
)
{
    /// <summary>Whether an app of <paramref name="kind"/> takes part: remove reaches every app.</summary>
    public bool Wants(HostKind kind) => Mode == McpMode.Remove || kind.Wanted(Cli, Desktop, All);
}
