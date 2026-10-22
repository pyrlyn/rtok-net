using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Text.Json;
using ConsoleAppFramework;
using Pyrlyn.Config.Layered;
using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Agents.Usage;
using Rtok.Config;
using Rtok.Mcp.Install;
using Rtok.Store;
using Tomlyn.Model;

namespace Rtok.Cli;

/// <summary>The agents rtok works with: install rtok into their hosts, report what each one has, and what they used.</summary>
internal sealed partial class AgentsCommands
{
    private readonly TextWriter _stdout = Console.Out;
    private readonly TextWriter _stderr = Console.Error;

    /// <summary>
    /// rtok's aliases, spelled out before routing: <c>agent</c> for <c>agents</c>, <c>setup</c> for
    /// <c>install</c>, <c>remove</c> for <c>uninstall</c>. ConsoleAppFramework routes a <c>|</c> alias
    /// only at the top level.
    /// </summary>
    public static string[] Aliases(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is not ["agents" or "agent", ..])
        {
            return args;
        }
        var spelled = (string[])args.Clone();
        spelled[0] = "agents";
        if (spelled.Length > 1)
        {
            spelled[1] = spelled[1] switch
            {
                "setup" => "install",
                "remove" => "uninstall",
                var verb => verb,
            };
        }
        return spelled;
    }

    /// <summary>Install rtok into hosts: its MCP server entry, keeping every other byte of their configs.</summary>
    /// <param name="host">The hosts, comma-separated: claude,codex.</param>
    /// <param name="dryRun">Print what would change; write nothing.</param>
    /// <param name="yes">Answer yes to every question.</param>
    /// <param name="mcp">Register rtok's MCP server.</param>
    /// <param name="cli">Only the CLI apps.</param>
    /// <param name="desktop">--gui, Only the desktop apps.</param>
    /// <param name="all">Every app of the host (the default without --cli or --desktop).</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("install")]
    public int Install(
        [Argument] string host,
        bool dryRun = false,
        bool yes = false,
        bool mcp = false,
        bool cli = false,
        bool desktop = false,
        bool all = false,
        string? config = null
    ) =>
        Apply(
            host,
            McpMode.Install,
            cli,
            desktop,
            all,
            config,
            Flags(
                ("setup.dry_run", dryRun, "--dry-run"),
                ("setup.yes", yes, "--yes"),
                ("setup.mcp", mcp, "--mcp")
            )
        );

    /// <summary>Take rtok out of hosts: only entries that are still rtok's; one you changed asks first.</summary>
    /// <param name="host">The hosts, comma-separated.</param>
    /// <param name="dryRun">Print what would change; write nothing.</param>
    /// <param name="yes">Remove entries you changed without asking.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("uninstall")]
    public int Uninstall(
        [Argument] string host,
        bool dryRun = false,
        bool yes = false,
        string? config = null
    ) =>
        Apply(
            host,
            McpMode.Remove,
            cli: false,
            desktop: false,
            all: true,
            config,
            Flags(("setup.dry_run", dryRun, "--dry-run"), ("setup.yes", yes, "--yes"))
        );

    /// <summary>Rewrite rtok's entries to what this rtok writes, in the hosts that have rtok (all of them when none is named).</summary>
    /// <param name="host">The hosts, comma-separated; every host with rtok when omitted.</param>
    /// <param name="dryRun">Print what would change; write nothing.</param>
    /// <param name="cli">Only the CLI apps.</param>
    /// <param name="desktop">--gui, Only the desktop apps.</param>
    /// <param name="all">Every app of the host (the default without --cli or --desktop).</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("update")]
    public int Update(
        [Argument] string? host = null,
        bool dryRun = false,
        bool cli = false,
        bool desktop = false,
        bool all = false,
        string? config = null
    ) =>
        Run(() =>
        {
            var loaded = Load(config, Flags(("setup.dry_run", dryRun, "--dry-run")));
            var run = new HostRun(Context(loaded));
            var hosts = host is null ? run.Installed() : AgentHosts.Parse(host);
            if (hosts.Count == 0)
            {
                _stdout.WriteLine(
                    "nothing to update: rtok is not installed in any host (rtok agents install <host>)"
                );
                return;
            }
            _stdout.Write(run.Run(new HostRequest(hosts, McpMode.Update, cli, desktop, all)));
        });

    /// <summary>Every host rtok knows: whether its apps are here, and what of rtok each one has.</summary>
    /// <param name="json">Print JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("list")]
    public int List(bool json = false, string? config = null) =>
        Run(() => Write(AgentHosts.All, json, config));

    /// <summary>One or more hosts in detail: apps, files, and each module's state.</summary>
    /// <param name="host">The hosts, comma-separated.</param>
    /// <param name="json">Print JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("info")]
    public int Info([Argument] string host, bool json = false, string? config = null) =>
        Run(() => Write(AgentHosts.Resolve(AgentHosts.Parse(host)), json, config));

    private void Write(IReadOnlyList<AgentHost> hosts, bool json, string? config)
    {
        var run = new HostRun(Context(Load(config, [])));
        if (json)
        {
            _stdout.WriteLine(
                JsonSerializer.Serialize(
                    run.Rows(hosts),
                    AgentsJson.Default.IReadOnlyListAgentListRow
                )
            );
            return;
        }
        _stdout.Write(run.List(hosts));
    }

    private int Apply(
        string host,
        McpMode mode,
        bool cli,
        bool desktop,
        bool all,
        string? config,
        IReadOnlyList<ConfigFlag> flags
    ) =>
        Run(() =>
        {
            var run = new HostRun(Context(Load(config, flags)));
            _stdout.Write(
                run.Run(new HostRequest(AgentHosts.Parse(host), mode, cli, desktop, all))
            );
        });

    private int Run(Action action)
    {
        try
        {
            action();
            return 0;
        }
        catch (Exception e) when (e is AgentsException or IOException or InvalidDataException)
        {
            _stderr.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    /// <summary>Only the flags given: one left off keeps the config's value.</summary>
    private static IReadOnlyList<ConfigFlag> Flags(
        params (string Key, bool On, string Flag)[] flags
    ) => [.. flags.Where(f => f.On).Select(f => new ConfigFlag(f.Key, true, f.Flag))];

    private LoadedConfig Load(string? config, IReadOnlyList<ConfigFlag> flags)
    {
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions { ConfigFile = config, Flags = flags }
        );
        foreach (var issue in loaded.Issues)
        {
            _stderr.WriteLine($"warning: {issue} (using the default)");
        }
        return loaded;
    }

    private HostContext Context(LoadedConfig loaded) =>
        HostContexts.Of(loaded, Console.IsInputRedirected ? null : Ask);

    private bool Ask(string question, bool defaultYes)
    {
        _stderr.Write(Prompts.Line(question, defaultYes));
        return Prompts.Answer(Console.ReadLine(), defaultYes);
    }

    /// <summary>
    /// Tokens and estimated cost per agent and month (or day), from the agents' logs or rtok.
    /// Prices come from [stats.prices]; a model without one counts in the tokens and is left out
    /// of the cost (--unpriced names those).
    /// </summary>
    /// <param name="source">Data source: logs (the agents' own session files, the default), rtok (what passed through rtok) or both.</param>
    /// <param name="host">Only these hosts, comma-separated (claude,codex).</param>
    /// <param name="since">From a date (2026-09-01, whole days in --tz) or a duration back from now (30d).</param>
    /// <param name="until">Through this date, inclusive.</param>
    /// <param name="daily">Bottom table by day.</param>
    /// <param name="monthly">Bottom table by month (the default).</param>
    /// <param name="by">Middle table grouping: agent (the default) or model.</param>
    /// <param name="tz">IANA time zone for day and month boundaries (default: the system zone).</param>
    /// <param name="unpriced">List the models without a price instead of the tables.</param>
    /// <param name="json">One JSON document.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("usage")]
    public void Usage(
        string? source = null,
        string? host = null,
        string? since = null,
        string? until = null,
        bool daily = false,
        bool monthly = false,
        string? by = null,
        string? tz = null,
        bool unpriced = false,
        bool json = false,
        string? config = null
    )
    {
        if (daily && monthly)
        {
            throw new ValidationException("--daily and --monthly cannot be used together");
        }
        var flags = new List<ConfigFlag>();
        void Flag(string key, object? value, string name)
        {
            if (value is not null)
            {
                flags.Add(new ConfigFlag($"agents.usage.{key}", value, name));
            }
        }
        Flag("source", source, "--source");
        Flag("hosts", host is null ? null : Hosts(host), "--host");
        Flag("since", since, "--since");
        Flag("until", until, "--until");
        Flag(
            "period",
            daily ? "daily"
                : monthly ? "monthly"
                : null,
            daily ? "--daily" : "--monthly"
        );
        Flag("by", by, "--by");
        Flag("tz", tz, "--tz");
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions { ConfigFile = config, Flags = flags }
        );
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        var report = Build(loaded.Config);
        foreach (var s in report.Skipped)
        {
            _stderr.WriteLine($"skipped {s.Host}: {s.Reason} in {s.Path}");
        }
        _stdout.Write(
            json ? UsageJson.Render(report) + "\n"
            : unpriced ? UsageText.Unpriced(report)
            : UsageText.Render(report)
        );
    }

    /// <summary>The report; the store is opened only when the source reads it.</summary>
    private static UsageReport Build(RtokConfig config)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (config.Agents.Usage.Source is "logs")
        {
            return UsageReports.Build(config, NoLedger.Instance, now);
        }
        using var store = RtokStore.Open(new StoreOptions(config.Core.DbPath));
        return UsageReports.Build(config, new StoreLedger(store), now);
    }

    private static TomlArray Hosts(string list)
    {
        var hosts = new TomlArray();
        foreach (var h in list.Split(','))
        {
            hosts.Add(h.Trim());
        }
        return hosts;
    }
}
