using System.Text;
using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>
/// <c>rtok agents install|update|uninstall|list|info</c> over the host table: one block per app
/// (rtok <c>agents::run</c>, <c>agents::block</c>). Only the modules built so far take part: MCP
/// now; hooks, proxy, plugin and restart come with T35.3–T35.6.
/// </summary>
/// <param name="ctx">The home, disk, binary and switches of this run.</param>
public sealed class HostRun(HostContext ctx)
{
    /// <summary>Printed before install and update when <c>rtok</c> is not on <c>PATH</c>.</summary>
    public const string NotOnPath =
        "warning: rtok is not on PATH; the hooks and MCP entries spawn `rtok` by name and will fail until it is";

    private enum Outcome
    {
        NotFound,
        Listed,
        Applied,
        Shared,
        NotInstalled,
    }

    /// <summary>Installs, updates or removes rtok in the hosts asked for and reports each app.</summary>
    /// <exception cref="AgentsException">A host is unknown, or (install, update) none of a host's wanted apps is here.</exception>
    /// <exception cref="IOException">A host file cannot be read.</exception>
    /// <exception cref="InvalidDataException">A host file does not parse, or rtok's entry cannot be put in or taken out.</exception>
    public string Run(HostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hosts = AgentHosts.Resolve(request.Hosts);
        if (request.Mode != McpMode.Remove)
        {
            var absent = hosts
                .Where(h => !h.Variants.Any(v => request.Wants(v.Kind) && ctx.Probe.Present(v)))
                .Select(h => h.Id)
                .ToList();
            if (absent.Count > 0)
            {
                throw new AgentsException($"{string.Join(", ", absent)} is not installed");
            }
        }
        var settings = ctx.Settings;
        var backups =
            !settings.DryRun && settings.Backup ? new BackupRun(settings.BackupFiles) : null;
        var apply = new Apply
        {
            DryRun = settings.DryRun,
            Backup = settings.Backup,
            BackupFiles = settings.BackupFiles,
            Yes = settings.Yes,
            Ask = settings.Ask,
            Run = backups,
        };
        var blocks = new StringBuilder();
        var changed = false;
        foreach (var host in hosts)
        {
            changed |= ApplyHost(host, request, apply, blocks);
        }
        backups?.Finish(changed);
        var text = new StringBuilder();
        if (request.Mode != McpMode.Remove && !ctx.RtokOnPath)
        {
            text.Append(NotOnPath).Append('\n');
        }
        if (changed && backups is not null)
        {
            foreach (var copy in backups.Copies)
            {
                text.Append("backup ").Append(copy).Append('\n');
            }
        }
        return text.Append(blocks).ToString();
    }

    /// <summary>The hosts with rtok in at least one present app, in table order: what <c>agents update</c> with no host updates.</summary>
    public IReadOnlyList<string> Installed() =>
        [
            .. AgentHosts
                .All.Where(h =>
                    h.Variants.Any(v =>
                        ctx.Probe.Present(v) && new McpModule(h, v, ctx).Installed()
                    )
                )
                .Select(h => h.Id),
        ];

    /// <summary>One block per app of <paramref name="hosts"/>: <c>agents list</c> and <c>agents info</c>.</summary>
    public string List(IReadOnlyList<AgentHost> hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        var text = new StringBuilder();
        foreach (var host in hosts)
        {
            foreach (var v in host.Variants)
            {
                text.Append(
                    Block(host, v, ctx.Probe.Present(v) ? Outcome.Listed : Outcome.NotFound)
                );
            }
        }
        return text.ToString();
    }

    /// <summary>One row per app of <paramref name="hosts"/>: <c>agents list --json</c> and <c>agents info --json</c>.</summary>
    /// <param name="hosts">The hosts.</param>
    /// <param name="versions">Ask each present app its <c>--version</c>; <c>rtok doctor</c> starts no process and passes false.</param>
    public IReadOnlyList<AgentListRow> Rows(IReadOnlyList<AgentHost> hosts, bool versions = true)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        return
        [
            .. hosts.SelectMany(h =>
                h.Variants.Select(v =>
                {
                    var mcp = new McpModule(h, v, ctx);
                    var app = ctx.Probe.AppPath(v);
                    var present = app is not null;
                    return new AgentListRow(
                        h.Id,
                        v.Kind.Word(),
                        v.Name,
                        present,
                        app,
                        present && versions ? ctx.Probe.Version(v) : null,
                        [.. mcp.Files.Distinct(StringComparer.Ordinal)],
                        present ? Modules(h, v, mcp) : [],
                        present ? mcp.Rows() : []
                    );
                })
            ),
        ];
    }

    /// <summary>Applies one host to each wanted app and appends their blocks; true when something changed.</summary>
    private bool ApplyHost(AgentHost host, HostRequest request, Apply apply, StringBuilder text)
    {
        string? done = null;
        var any = false;
        var changed = false;
        foreach (var v in host.Variants.Where(v => request.Wants(v.Kind)))
        {
            any = true;
            if (request.Mode != McpMode.Remove && !ctx.Probe.Present(v))
            {
                text.Append(Block(host, v, Outcome.NotFound));
                continue;
            }
            if (done is not null && host.Shared)
            {
                text.Append(Block(host, v, Outcome.Shared));
                continue;
            }
            var mcp = new McpModule(host, v, ctx);
            if (request.Mode == McpMode.Update && !mcp.Installed())
            {
                text.Append(Block(host, v, Outcome.NotInstalled));
                continue;
            }
            var reports = mcp.Apply(request.Mode, apply);
            done = v.Name;
            changed |= reports.Any(r =>
                !string.Equals(r, Apply.NoChanges, StringComparison.Ordinal)
            );
            text.Append(Block(host, v, Outcome.Applied, request.Mode, reports));
            if (
                request.Mode != McpMode.Remove
                && !ctx.Settings.DryRun
                && Expected(host, v)
                && !mcp.Installed()
            )
            {
                text.Append("  warning: mcp did not read back as installed\n");
            }
        }
        if (!any)
        {
            text.Append("skip ").Append(host.Id).Append(": not selected\n");
        }
        return changed;
    }

    /// <summary>Whether an install should leave rtok's MCP entry on this app.</summary>
    private bool Expected(AgentHost host, HostVariant v) =>
        ctx.Settings.Mcp && host.Mcp.Of(v.Kind).Level == SupportLevel.Yes;

    private string Block(
        AgentHost host,
        HostVariant v,
        Outcome outcome,
        McpMode mode = McpMode.Install,
        IReadOnlyList<string>? reports = null
    )
    {
        reports ??= [];
        var unchanged = reports.All(r =>
            string.Equals(r, Apply.NoChanges, StringComparison.Ordinal)
        );
        var note = outcome switch
        {
            Outcome.NotFound => " — not found",
            Outcome.Applied when ctx.Settings.DryRun => " — dry run, nothing written",
            Outcome.Applied when unchanged => mode switch
            {
                McpMode.Remove => " — no changes",
                McpMode.Update => " — already current",
                _ => " — already installed",
            },
            Outcome.Shared => " — same files as above",
            Outcome.NotInstalled => " — not installed",
            _ => "",
        };
        var text = new StringBuilder($"{v.Kind.Label()}: {v.Name}{note}\n");
        if (outcome == Outcome.NotInstalled)
        {
            return text.Append(
                    $"  skip    nothing of rtok here; run `rtok agents install {host.Id}`\n"
                )
                .ToString();
        }
        text.Append(
            ctx.Probe.AppPath(v) is { } app
                ? $"  app     {app} ({ctx.Probe.Version(v)})\n"
                : "  app     -\n"
        );
        var mcp = new McpModule(host, v, ctx);
        var files = mcp.Files.Distinct(StringComparer.Ordinal).ToList();
        if (files.Count > 0)
        {
            text.Append("  config  ").AppendJoin(", ", files).Append('\n');
        }
        if (outcome == Outcome.NotFound)
        {
            return text.ToString();
        }
        foreach (
            var report in reports.Where(r =>
                !string.Equals(r, Apply.NoChanges, StringComparison.Ordinal)
            )
        )
        {
            text.Append(report).Append('\n');
        }
        AppendModules(text, host, v, mcp);
        return text.ToString();
    }

    /// <summary>One line per module; MCP gets one line per surface when it has files.</summary>
    private static void AppendModules(
        StringBuilder text,
        AgentHost host,
        HostVariant v,
        McpModule mcp
    )
    {
        var rows = mcp.Rows();
        foreach (var row in Modules(host, v, mcp))
        {
            if (string.Equals(row.Name, McpModule.Name, StringComparison.Ordinal) && rows.Count > 0)
            {
                foreach (var r in rows)
                {
                    var state = r.Serves ? ModuleState.Installed : ModuleState.NotInstalled;
                    text.Append("  ")
                        .Append(state.Mark())
                        .Append($"mcp     {r.Surface}  {r.Text()}\n");
                }
                continue;
            }
            text.Append("  ").Append(row.State.Mark()).Append(row.Text()).Append('\n');
        }
    }

    /// <summary>The rows of the modules this build installs: MCP.</summary>
    private static IReadOnlyList<ModuleRow> Modules(AgentHost host, HostVariant v, McpModule mcp)
    {
        var support = host.Mcp.Of(v.Kind);
        var row = mcp.Installed()
            ? new ModuleRow(McpModule.Name, ModuleState.Installed, "")
            : support.Level switch
            {
                SupportLevel.Yes => new ModuleRow(McpModule.Name, ModuleState.NotInstalled, ""),
                SupportLevel.Flag or SupportLevel.Offer => new ModuleRow(
                    McpModule.Name,
                    ModuleState.NotInstalled,
                    $" ({support.Note})"
                ),
                _ => new ModuleRow(McpModule.Name, ModuleState.NotSupported, $": {support.Note}"),
            };
        return [row];
    }
}
