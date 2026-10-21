using Rtok.Agents.Hosts;
using Rtok.Config;
using Rtok.Plugin.Sdk;
using Rtok.Store;

namespace Rtok.Model;

/// <summary>Where each page of a <see cref="Snapshot"/> comes from. A page whose data has no
/// source yet reads a <see cref="PendingSource"/>; swapping in the real one changes nothing
/// else.</summary>
public sealed record ModelSources(
    IPageSource<OverviewPage> Overview,
    IPageSource<IReadOnlyList<PluginRow>> Plugins,
    IPageSource<IReadOnlyList<CallItem>> Calls,
    IPageSource<IReadOnlyList<SessionItem>> Sessions,
    IPageSource<TextPage> Doctor,
    IPageSource<TextPage> Logs,
    IPageSource<TextPage> Memory,
    IPageSource<TextPage> Graph,
    IPageSource<IReadOnlyList<ConfigRow>> Config,
    IPageSource<TextPage> Services,
    IPageSource<TextPage> Worktrees,
    IPageSource<TextPage> Hosts,
    IPageSource<TextPage> Stats,
    IPageSource<TextPage> Usage,
    IPageSource<TextPage> Tasks
)
{
    /// <summary>How long a host probe stays fresh.</summary>
    public static readonly TimeSpan HostsTtl = TimeSpan.FromSeconds(30);

    /// <summary>How long a usage report stays fresh.</summary>
    public static readonly TimeSpan UsageTtl = TimeSpan.FromSeconds(120);

    /// <summary>How long a worktree scan stays fresh.</summary>
    public static readonly TimeSpan WorktreesTtl = TimeSpan.FromSeconds(300);

    private const string NotYet = "not available in this build yet\n";

    /// <summary>The sources this build has: the store, the config, the linked plugins and the
    /// agent hosts.</summary>
    /// <param name="store">The store.</param>
    /// <param name="linked">The plugins this build links.</param>
    /// <param name="hosts">The host context for a config; <c>c =&gt; HostContexts.Of(c, null)</c>
    /// on a real machine.</param>
    /// <returns>The sources.</returns>
    public static ModelSources Create(
        RtokStore store,
        Func<IEnumerable<IPlugin>> linked,
        Func<LoadedConfig, HostContext> hosts
    ) =>
        new(
            new OverviewSource(store),
            new PluginsSource(linked),
            new CallsSource(store),
            new SessionsSource(store),
            new PendingSource(NotYet),
            new LogsSource(),
            new PendingSource(NotYet),
            new PendingSource(NotYet),
            new ConfigPageSource(),
            new PendingSource(NotYet),
            new BackgroundSource<TextPage>(
                new PendingSource(NotYet),
                new TextPage("reading worktrees…\n", Ready: false),
                WorktreesTtl
            ),
            new BackgroundSource<TextPage>(
                new HostsSource(hosts),
                new TextPage("probing hosts…\n", Ready: false),
                HostsTtl
            ),
            new PendingSource(NotYet),
            new BackgroundSource<TextPage>(
                new UsageSource(store),
                new TextPage("reading usage…\n", Ready: false),
                UsageTtl
            ),
            new PendingSource(NotYet)
        );

    /// <summary>Reads every page once with one config.</summary>
    /// <param name="context">The config and clock of this build.</param>
    /// <returns>The snapshot.</returns>
    public Snapshot Build(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Snapshot(
            context.Time.GetUtcNow(),
            Overview.Read(context),
            Plugins.Read(context),
            Calls.Read(context),
            Sessions.Read(context),
            Doctor.Read(context),
            Logs.Read(context),
            Memory.Read(context),
            Graph.Read(context),
            Config.Read(context),
            Services.Read(context),
            Worktrees.Read(context),
            Hosts.Read(context),
            Stats.Read(context),
            Usage.Read(context),
            Tasks.Read(context)
        );
    }
}
