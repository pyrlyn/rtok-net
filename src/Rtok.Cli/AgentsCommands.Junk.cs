using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Pyrlyn.Config.Layered;
using Pyrlyn.Durations;
using Pyrlyn.FsKit;
using Rtok.Agents.Hosts;
using Rtok.Agents.Junk;
using Rtok.Config;
using Rtok.Hook.Contracts;
using Rtok.Mcp.Install;
using Rtok.Store;

namespace Rtok.Cli;

/// <summary><c>rtok agents junk list|clear</c>: the space agents leave behind, and its removal.</summary>
internal sealed partial class AgentsCommands
{
    /// <summary>Per agent: its folders and sizes, the junk kinds in them, and what clear would free; read-only.</summary>
    /// <param name="json">One JSON document.</param>
    /// <param name="bytes">Exact byte counts instead of KB, MB and GB.</param>
    /// <param name="all">Also the hosts that are not installed.</param>
    /// <param name="items">Items shown per kind: a count, all, or 0 for totals only (default 10).</param>
    /// <param name="sort">Item order: size (the default), last-used or path.</param>
    /// <param name="minSize">Only items at least this big: 10M, 512K, 4096.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("junk list")]
    public int JunkList(
        bool json = false,
        bool bytes = false,
        bool all = false,
        string? items = null,
        string? sort = null,
        string? minSize = null,
        string? config = null
    )
    {
        var view = View(items, sort, minSize, bytes);
        return Run(() =>
        {
            var (loaded, ctx) = JunkConfig(config, []);
            using var store = OpenStore(loaded.Config);
            var report = JunkScan.Build(JunkInputs(loaded.Config, ctx, all, store));
            view = view with { Days = loaded.Config.Agents.Junk.StaleSessionDays };
            _stdout.Write(
                json
                    ? Agents.Junk.JunkList.Json(report, view) + "\n"
                    : Agents.Junk.JunkList.Text(report, view)
            );
        });
    }

    /// <summary>Remove agent junk, each item checked again first: a dry run unless --yes; exit 1 when some stays.</summary>
    /// <remarks>
    /// Without a selecting flag only rtok's own old logs and archives go; --kind, --agent,
    /// --include review and --older-than choose more.
    /// </remarks>
    /// <param name="yes">Remove; without it nothing changes.</param>
    /// <param name="include">review: also the review kinds (logs, deps, backups, index, stale-worktrees).</param>
    /// <param name="kind">Only these kinds, comma-separated: cache,temp. sessions only when named.</param>
    /// <param name="agent">Only these agents, comma-separated: rtok or host ids.</param>
    /// <param name="olderThan">Only items whose newest file is at least this old: 7d, 12h.</param>
    /// <param name="trash">Move to the OS trash instead of removing.</param>
    /// <param name="sessionDays">Sessions older than this many days are junk (agents.junk.stale_session_days).</param>
    /// <param name="minSize">Only items at least this big: 10M, 512K, 4096.</param>
    /// <param name="json">One JSON document.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("junk clear")]
    public int JunkClear(
        bool yes = false,
        string? include = null,
        string? kind = null,
        string? agent = null,
        string? olderThan = null,
        bool trash = false,
        uint? sessionDays = null,
        string? minSize = null,
        bool json = false,
        string? config = null
    )
    {
        var (filter, view) = ClearFilter(include, kind, agent, olderThan, trash, minSize);
        ConfigFlag[] flags = sessionDays is { } d
            ? [new("agents.junk.stale_session_days", d, "--session-days")]
            : [];
        var failed = false;
        var code = Run(() =>
        {
            var (loaded, ctx) = JunkConfig(config, flags);
            using var store = OpenStore(loaded.Config);
            var inputs = JunkInputs(loaded.Config, ctx, false, store);
            var cleared = Agents.Junk.JunkClear.Run(
                new JunkClearOptions
                {
                    Config = loaded.Config,
                    Report = JunkScan.Build(inputs),
                    Filter = filter,
                    View = view with
                    {
                        Now = inputs.Context.Now,
                        Days = loaded.Config.Agents.Junk.StaleSessionDays,
                    },
                    Protected = Agents.Junk.JunkClear.Protected(
                        loaded.Config,
                        inputs.Roots,
                        inputs.Hosts.SelectMany(h => h.Markers)
                    ),
                    Store = store is null ? null : new StoreJunk(store),
                    Running = id => !ctx.Env.Sandboxed && HostRunning(id),
                },
                yes
            );
            _stdout.Write(
                json
                    ? Agents.Junk.JunkClear.Json(cleared) + "\n"
                    : Agents.Junk.JunkClear.Text(cleared, view)
            );
            if (cleared.Failed)
            {
                _stderr.WriteLine("error: some junk could not be removed");
                failed = true;
            }
        });
        return code != 0 ? code
            : failed ? 1
            : 0;
    }

    /// <summary>The <c>clear</c> flags, each refused as a usage error.</summary>
    private static (JunkFilter Filter, JunkView View) ClearFilter(
        string? include,
        string? kind,
        string? agent,
        string? olderThan,
        bool trash,
        string? minSize
    )
    {
        var view = View(null, null, minSize, false);
        if (include is not (null or "review"))
        {
            throw new ValidationException($"--include takes review, not {include}");
        }
        var kinds = List(kind);
        if (kinds.Select(JunkFilter.KindError).OfType<string>().FirstOrDefault() is { } badKind)
        {
            throw new ValidationException(badKind);
        }
        var agents = List(agent);
        var ids = AgentHosts.All.Select(h => h.Id).ToList();
        if (
            agents.Select(a => JunkFilter.AgentError(a, ids)).OfType<string>().FirstOrDefault() is
            { } badAgent
        )
        {
            throw new ValidationException(badAgent);
        }
        TimeSpan? age = null;
        if (olderThan is not null)
        {
            try
            {
                age = HumanDuration.Parse(olderThan);
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                throw new ValidationException($"--older-than: {e.Message}", e);
            }
        }
        return (
            new JunkFilter
            {
                Agents = agents,
                Kinds = kinds,
                IncludeReview = include is "review",
                OlderThan = age,
                Trash = trash,
                MinSize = view.MinSize,
            },
            view
        );
    }

    private static List<string> List(string? csv) =>
        csv is null
            ? []
            :
            [
                .. csv.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
            ];

    private static JunkView View(string? items, string? sort, string? minSize, bool bytes)
    {
        var shown = JunkView.DefaultShown;
        if (items is not null)
        {
            shown =
                JunkView.ParseShown(items)
                ?? throw new ValidationException($"--items takes a count or all, not {items}");
        }
        if (sort is not (null or "size" or "last-used" or "path"))
        {
            throw new ValidationException($"--sort takes size, last-used or path, not {sort}");
        }
        long min = 0;
        if (minSize is not null)
        {
            min =
                JunkView.ParseSize(minSize)
                ?? throw new ValidationException(
                    $"--min-size takes a size like 10M, not {minSize}"
                );
        }
        var home = HostEnvironment.Current().Home;
        return new JunkView
        {
            Shown = shown,
            Sort = sort ?? "size",
            MinSize = min,
            Exact = bytes,
            Links = !Console.IsOutputRedirected,
            Now = DateTime.UtcNow,
            Home = home,
        };
    }

    /// <summary>The config, refused on any issue: a junk run judges by it.</summary>
    private (LoadedConfig Loaded, HostContext Context) JunkConfig(
        string? config,
        IReadOnlyList<ConfigFlag> flags
    )
    {
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions { ConfigFile = config, Flags = flags }
        );
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        return (loaded, Context(loaded));
    }

    /// <summary>The store, or null when it does not exist yet or cannot be opened: then there are no archives.</summary>
    private static RtokStore? OpenStore(RtokConfig config)
    {
        if (!File.Exists(config.Core.DbPath))
        {
            return null;
        }
        try
        {
            return RtokStore.Open(new StoreOptions(config.Core.DbPath));
        }
        catch (Exception e)
            when (e is IOException or InvalidOperationException or System.Data.Common.DbException)
        {
            return null;
        }
    }

    private static JunkOptions JunkInputs(
        RtokConfig config,
        HostContext ctx,
        bool all,
        RtokStore? store
    )
    {
        var home = ctx.Env.Home;
        return new JunkOptions
        {
            Config = config,
            RtokHome = RtokHome.Resolve(),
            Roots = new JunkRoots(home, ctx.Env.Variable, JunkRoots.CurrentOs()),
            Hosts = [.. AgentHosts.All.Select(h => JunkHostOf(h, ctx))],
            Context = new JunkContext(
                FsPath.Canon(Environment.CurrentDirectory),
                JunkContext.DefaultIdle,
                DateTime.UtcNow,
                JunkContext.ScanLimit
            ),
            All = all,
            Store = store is null ? null : new StoreJunk(store),
        };
    }

    private static JunkHost JunkHostOf(AgentHost host, HostContext ctx) =>
        new(
            host.Id,
            host.Variants.Any(ctx.Probe.Present),
            [.. McpHosts.For(host.Id, ctx.McpEnv, ctx.Fs).Select(s => s.Path)],
            ProcessNames(host)
        );

    /// <summary>A host's binaries and app names, lowercase: what its process and crash reports are named.</summary>
    private static List<string> ProcessNames(AgentHost host) =>
        [
            .. host
                .Variants.SelectMany(v =>
                    v.Bins.Concat(
                        v.Apps.Select(a => Path.GetFileNameWithoutExtension(a.TrimEnd('/', '\\')))
                    )
                )
                .Where(n => n.Length > 0)
                .Select(n => n.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal),
        ];

    /// <summary>One of the host's binaries or apps runs now.</summary>
    private static bool HostRunning(string id)
    {
        if (AgentHosts.Find(id) is not { } host)
        {
            return false;
        }
        var names = ProcessNames(host).ToHashSet(StringComparer.Ordinal);
        var processes = System.Diagnostics.Process.GetProcesses();
        try
        {
            return processes.Any(p =>
            {
                try
                {
                    return names.Contains(p.ProcessName.ToLowerInvariant());
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });
        }
        finally
        {
            Array.ForEach(processes, p => p.Dispose());
        }
    }
}
