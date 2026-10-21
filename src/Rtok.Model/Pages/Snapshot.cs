namespace Rtok.Model;

/// <summary>
/// One operator snapshot: every page as plain data, built off-thread and handed whole to the web
/// dashboard and the TUI. Surfaces read only this, never the store.
/// </summary>
/// <param name="BuiltAt">When the build finished.</param>
public sealed record Snapshot(
    DateTimeOffset BuiltAt,
    OverviewPage Overview,
    IReadOnlyList<PluginRow> Plugins,
    IReadOnlyList<CallItem> Calls,
    IReadOnlyList<SessionItem> Sessions,
    TextPage Doctor,
    TextPage Logs,
    TextPage Memory,
    TextPage Graph,
    IReadOnlyList<ConfigRow> Config,
    TextPage Services,
    TextPage Worktrees,
    TextPage Hosts,
    TextPage Stats,
    TextPage Usage,
    TextPage Tasks
);
