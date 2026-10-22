using Rtok.Config;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>Everything the report reads from the store, once.</summary>
public sealed record ReportFacts
{
    /// <summary>The window.</summary>
    public required ReportWindow Window { get; init; }

    /// <summary><c>calls</c> rows in the window.</summary>
    public long Calls { get; init; }

    /// <summary>All <c>calls</c> rows.</summary>
    public long CallsTotal { get; init; }

    /// <summary><c>measurements</c> rows in the window.</summary>
    public long Measurements { get; init; }

    /// <summary>All <c>measurements</c> rows.</summary>
    public long MeasurementsTotal { get; init; }

    /// <summary><c>usage</c> rows in the window.</summary>
    public long Usage { get; init; }

    /// <summary>All <c>usage</c> rows.</summary>
    public long UsageTotal { get; init; }

    /// <summary>Net saving per plugin, by plugin id.</summary>
    public IReadOnlyList<PluginSaving> Savings { get; init; } = [];

    /// <summary>hook, mcp and proxy, always all three.</summary>
    public IReadOnlyList<SurfaceLatency> Surfaces { get; init; } = [];

    /// <summary>Hook events, most calls first.</summary>
    public IReadOnlyList<HookActivity> Hooks { get; init; } = [];

    /// <summary>Prompt-cache health per session with usage rows in the window.</summary>
    public IReadOnlyList<CacheHealth.Session> Cache { get; init; } = [];

    /// <summary>The archive pointers and their expansions.</summary>
    public ExpandFacts Expand { get; init; } = new(0, 0, 0, 0, []);

    /// <summary>The ten costliest actionable sinks.</summary>
    public IReadOnlyList<Sink> Sinks { get; init; } = [];

    /// <summary>The config values the rules compare with.</summary>
    public required AdviceSettings Settings { get; init; }

    /// <summary>Reads the facts of <paramref name="window"/> from <paramref name="store"/>.</summary>
    public static ReportFacts Read(RtokStore store, ReportWindow window, AdviceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(window);
        var since = window.From.ToUnixTimeSeconds();
        var (calls, callsTotal) = store.CallCounts(since);
        var (measurements, measurementsTotal, usage, usageTotal) = store.LedgerCounts(since);
        var rows = store.MeasurementsSince(since);
        var timings = store.CallTimings(since);
        var (decisions, expanded) = store.ArchiveDecisionCounts(since);
        var expands = rows.Where(r => r.Kind is "expand").ToList();
        return new ReportFacts
        {
            Window = window,
            Calls = calls,
            CallsTotal = callsTotal,
            Measurements = measurements,
            MeasurementsTotal = measurementsTotal,
            Usage = usage,
            UsageTotal = usageTotal,
            Savings = Fold(rows),
            Surfaces = Latencies(timings),
            Hooks = HookActivities(timings),
            Cache = CacheHealth.Report(store, since),
            Expand = new ExpandFacts(
                decisions,
                expanded,
                Saturating.Sum(expands.Select(r => r.EstAfter)),
                expands.Count,
                [.. expands.Select(r => r.RefId).OfType<string>()]
            ),
            Sinks = SinkRanking.Top(rows),
            Settings = settings,
        };
    }

    private static List<PluginSaving> Fold(IReadOnlyList<MeasurementInput> rows) =>
        [
            .. rows.GroupBy(r => r.Plugin, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new PluginSaving(
                    g.Key,
                    g.Count(),
                    Saturating.Sum(g.Select(r => r.EstBefore)),
                    Saturating.Sum(g.Select(r => r.EstAfter))
                )),
        ];

    // The surfaces whose calls carry a duration, in report order.
    private static readonly string[] TimedSurfaces = ["hook", "mcp", "proxy"];

    private static List<SurfaceLatency> Latencies(IReadOnlyList<CallTiming> timings) =>
        [
            .. TimedSurfaces.Select(surface =>
            {
                var calls = timings
                    .Where(t => string.Equals(t.Surface, surface, StringComparison.Ordinal))
                    .ToList();
                var ms = calls.Select(t => t.Ms).OfType<double>().Order().ToList();
                return new SurfaceLatency(
                    surface,
                    calls.Count,
                    ms.Count,
                    NearestRank(ms, 0.50),
                    NearestRank(ms, 0.95)
                );
            }),
        ];

    private static List<HookActivity> HookActivities(IReadOnlyList<CallTiming> timings) =>
        [
            .. timings
                .Where(t => t.Surface is "hook")
                .GroupBy(t => t.Name ?? "unknown", StringComparer.Ordinal)
                .Select(g => new HookActivity(g.Key, g.Count(), g.Count(t => t.Measured)))
                .OrderByDescending(h => h.Calls)
                .ThenBy(h => h.Name, StringComparer.Ordinal),
        ];

    /// <summary>The nearest-rank percentile of sorted values: index ceil(p·n) − 1.</summary>
    internal static double? NearestRank(IReadOnlyList<double> sorted, double p) =>
        sorted.Count == 0
            ? null
            : sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
}
