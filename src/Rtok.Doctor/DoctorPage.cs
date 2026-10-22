using Rtok.Agents.Hosts;

namespace Rtok.Doctor;

/// <summary><c>rtok doctor</c>: one read-only look at the hooks, MCP servers and audits of this machine.</summary>
public static class DoctorPage
{
    /// <summary>Scans everything and builds the report; writes nothing and starts only the probes in <paramref name="options"/>.</summary>
    /// <param name="options">The home, config and probes.</param>
    /// <returns>The report.</returns>
    public static DoctorReport Page(DoctorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var home = new DoctorHome(options);
        var findings = Findings.Scan(home);
        var claude = new ClaudeProbes(home);
        var servers = claude.McpServers();
        var search = claude.ToolSearch();
        var proxy = claude.Chain(claude.AnthropicBase());
        var rows = new HostRun(options.Hosts).Rows(AgentHosts.All, versions: false);
        var overlaps = Overlaps.Find(
            home,
            claude,
            findings.Hooks,
            rows,
            claude.AlwaysLoad(search, servers)
        );
        var exposure = Exposure.Find(home);
        var problems = findings.WithSelection();
        var hooks = findings.Hooks.Entries.Where(e => e.Agent is "claude").ToList();
        return new DoctorReport
        {
            HooksTotal = hooks.Count,
            HooksByEvent = ByEvent(hooks),
            HooksByTool = ByTool(hooks),
            Mcp = servers,
            Proxy = proxy,
            ProxyOpenai = claude.Chain(claude.OpenaiBase()),
            McpToolSearchDisabled = search.State is not "enabled",
            McpToolSearch = search,
            BashMaxOutputLength = home.Var("BASH_MAX_OUTPUT_LENGTH"),
            AutoCompactWindow = claude.Settings?["autoCompactWindow"]?.ToJsonString(),
            Instructions = options.Config.Doctor.Instructions
                ? InstructionScan.Audit(home, findings.Hooks, claude)
                : null,
            Skills = SkillScan.Audit(home, findings.Hooks.PluginIndex),
            Overlaps = overlaps.Count == 0 ? null : overlaps,
            ToolsRewriteAdvice = claude.ToolsRewrite(search, proxy, servers.Sum(s => s.DescTokens)),
            Agents = Agents(rows),
            Exposure = exposure.Count == 0 ? null : exposure,
            Problems = problems.Count == 0 ? null : problems,
            Fixable = problems.Count(p => p.Selected),
        };
    }

    private static SortedDictionary<string, int> ByEvent(List<HookEntry> hooks) =>
        new(
            hooks
                .GroupBy(h => h.Event, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            StringComparer.Ordinal
        );

    private static SortedDictionary<string, SortedDictionary<string, int>> ByTool(
        List<HookEntry> hooks
    ) =>
        new(
            hooks
                .Where(h => h.Matcher is not null)
                .GroupBy(h => h.Event, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => new SortedDictionary<string, int>(
                        g.GroupBy(h => h.Matcher!, StringComparer.Ordinal)
                            .ToDictionary(m => m.Key, m => m.Count(), StringComparer.Ordinal),
                        StringComparer.Ordinal
                    ),
                    StringComparer.Ordinal
                ),
            StringComparer.Ordinal
        );

    private static List<AgentRow> Agents(IEnumerable<AgentListRow> rows) =>
        [
            .. rows.Where(r => r.Present)
                .Select(r => new AgentRow(
                    r.Host,
                    r.Kind,
                    [
                        .. r.Modules.Select(m => new AgentModule(
                            m.Name,
                            m.State.Word().Replace(' ', '_'),
                            m.Note
                        )),
                    ]
                )),
        ];

    /// <summary>The report with only <paramref name="agent"/>'s problems; every other section stays.</summary>
    /// <param name="report">The full report.</param>
    /// <param name="agent">A host id.</param>
    /// <returns>The filtered report.</returns>
    public static DoctorReport ForAgent(DoctorReport report, string agent)
    {
        ArgumentNullException.ThrowIfNull(report);
        var problems = report
            .Problems?.Where(p => string.Equals(p.Agent, agent, StringComparison.Ordinal))
            .ToList();
        return report with
        {
            Problems = problems is { Count: > 0 } ? problems : null,
            Fixable = problems?.Count(p => p.Selected) ?? 0,
        };
    }
}
