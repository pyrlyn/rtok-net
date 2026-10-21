using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Rtok.Agents.Usage;
using Rtok.Config;
using Rtok.Store;
using Tomlyn.Model;

namespace Rtok.Cli;

/// <summary>The agents rtok works with.</summary>
internal sealed class AgentsCommands
{
    private readonly TextWriter _stdout = Console.Out;
    private readonly TextWriter _stderr = Console.Error;

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
