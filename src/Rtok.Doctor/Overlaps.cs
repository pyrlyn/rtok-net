using Rtok.Agents.Hosts;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>Where rtok and a host do the same job, and the rtok key that turns rtok's side off.</summary>
internal static class Overlaps
{
    public static List<string> Find(
        DoctorHome home,
        ClaudeProbes claude,
        HookScan hooks,
        IReadOnlyList<AgentListRow> agents,
        string? alwaysLoad
    )
    {
        var lines = new List<string>();
        var plugins = home.Config.Plugins;
        var recall = plugins.Memory.Enabled && plugins.Memory.RecallTokens > 0;
        if (claude.Settings is not null && recall)
        {
            lines.Add(
                "duplicate: Claude Code auto-memory (on by default) and memory recall both carry facts — rtok side: [plugins.memory] enabled = false"
            );
        }
        if (recall && SyncBlock(home))
        {
            lines.Add(
                "duplicate: a memory sync block and hook recall both carry titles — rtok side: [plugins.memory] enabled = false"
            );
        }
        if (plugins.Archive.Enabled && home.Fs.Directory.Exists(home.InHome(".config/opencode")))
        {
            lines.Add(
                "duplicate: OpenCode marks old tool outputs natively — rtok side: [plugins.archive] enabled = false"
            );
        }
        if (plugins.Archive.Enabled && home.Fs.Directory.Exists(home.InHome(".cursor")))
        {
            lines.Add(
                "duplicate: Cursor Dynamic Context prunes old context too — rtok side: [plugins.archive] enabled = false"
            );
        }
        if (home.Config.Setup.Mcp)
        {
            lines.AddRange(Mcp(agents));
        }
        if (alwaysLoad is not null)
        {
            lines.Add(alwaysLoad);
        }
        if (
            hooks.Entries.Exists(e =>
                e.Agent is "claude" && e.Command.Contains("rtok-hook", StringComparison.Ordinal)
            ) && home.Hosts.Probe.FindOnPath("rtok-hook") is null
        )
        {
            lines.Add(
                "hook client: `rtok-hook` is not on PATH, so hooks fall back to the slower `rtok hook`; install it beside `rtok` (a ketch install links both)"
            );
        }
        return lines;
    }

    /// <summary>rtok's memory markers in the project's CLAUDE.md or AGENTS.md.</summary>
    private static bool SyncBlock(DoctorHome home) =>
        ((string[])["CLAUDE.md", "AGENTS.md"]).Any(f =>
            home.TryRead(home.Under(home.Cwd, f)) is { } text
            && text.Contains("<!-- rtok:memory -->", StringComparison.Ordinal)
            && text.Contains("<!-- /rtok:memory -->", StringComparison.Ordinal)
        );

    /// <summary>An installed host whose MCP file lacks rtok's server, or holds a stale one.</summary>
    private static IEnumerable<string> Mcp(IReadOnlyList<AgentListRow> agents)
    {
        foreach (
            var row in agents.Where(r =>
                r.Modules.Any(m => m.Name is not "mcp" && m.State == ModuleState.Installed)
            )
        )
        {
            var flag = agents
                .Where(r => string.Equals(r.Host, row.Host, StringComparison.Ordinal))
                .Skip(1)
                .Any()
                ? $" --{row.Kind}"
                : "";
            var fix = $" — rtok side: rtok agents install {row.Host}{flag}";
            foreach (var mcp in row.Mcp)
            {
                var line = (mcp.Entry, mcp.Diff) switch
                {
                    (McpEntryState.Stale, var diff) => $"stale: {mcp.File} {diff}",
                    _ when mcp.Found => null,
                    (_, { } error) => $"missing: {mcp.File} ({error})",
                    _ => $"missing: {mcp.File} has no \"rtok\"",
                };
                if (line is not null)
                {
                    yield return line + fix;
                }
            }
        }
    }
}
