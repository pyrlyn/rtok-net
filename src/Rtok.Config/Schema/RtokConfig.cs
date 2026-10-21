namespace Rtok.Config;

/// <summary>Every setting rtok has, one table per section of the config file</summary>
public sealed class RtokConfig
{
    /// <summary>The [core] table.</summary>
    public CoreSection Core { get; set; } = new();

    /// <summary>The [estimator] table.</summary>
    public EstimatorSection Estimator { get; set; } = new();

    /// <summary>The [log] table.</summary>
    public LogSection Log { get; set; } = new();

    /// <summary>The [hook] table.</summary>
    public HookSection Hook { get; set; } = new();

    /// <summary>The [agents] table.</summary>
    public AgentsSection Agents { get; set; } = new();

    /// <summary>The [mcp] table.</summary>
    public McpSection Mcp { get; set; } = new();

    /// <summary>The [proxy] table.</summary>
    public ProxySection Proxy { get; set; } = new();

    /// <summary>The [web] table.</summary>
    public WebSection Web { get; set; } = new();

    /// <summary>The [tui] table.</summary>
    public TuiSection Tui { get; set; } = new();

    /// <summary>The [ui] table.</summary>
    public UiSection Ui { get; set; } = new();

    /// <summary>The [demon] table.</summary>
    public DemonSection Demon { get; set; } = new();

    /// <summary>The [stats] table.</summary>
    public StatsSection Stats { get; set; } = new();

    /// <summary>The [report] table.</summary>
    public ReportSection Report { get; set; } = new();

    /// <summary>The [bench] table.</summary>
    public BenchSection Bench { get; set; } = new();

    /// <summary>The [doctor] table.</summary>
    public DoctorSection Doctor { get; set; } = new();

    /// <summary>The [setup] table.</summary>
    public SetupSection Setup { get; set; } = new();

    /// <summary>The [expand] table.</summary>
    public ExpandSection Expand { get; set; } = new();

    /// <summary>The [filter] table.</summary>
    public FilterSection Filter { get; set; } = new();

    /// <summary>The [worktree] table.</summary>
    public WorktreeSection Worktree { get; set; } = new();

    /// <summary>The [tasks] table.</summary>
    public TasksSection Tasks { get; set; } = new();

    /// <summary>The [otel] table.</summary>
    public OtelSection Otel { get; set; } = new();

    /// <summary>The [plugins] table.</summary>
    public PluginsSection Plugins { get; set; } = new();
}
