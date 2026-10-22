using Pyrlyn.Durations;
using Rtok.Config;
using Rtok.Core;
using Rtok.Store;

namespace Rtok.Measure;

/// <summary>
/// The <c>rtok stats</c> pages. The transcript report needs no store: one that will not open
/// costs it the store's rows and nothing else. <c>--cache</c> and a catalogue plugin's page are
/// the store's rows alone, so there an unreadable store is an error.
/// </summary>
public static class StatsPage
{
    /// <summary>The text <c>rtok stats</c> prints, ending in a newline.</summary>
    /// <param name="config">The loaded config; <c>[stats]</c> carries the flags merged in.</param>
    /// <param name="home">The rtok home, where baselines live under <c>measurements/</c>.</param>
    /// <param name="request">The flags that are not config keys.</param>
    /// <param name="signals">What the plugins tell the report; <see cref="StatsSignals.None"/> without them.</param>
    /// <param name="now">The window's end.</param>
    public static string Render(
        RtokConfig config,
        string home,
        StatsRequest request,
        StatsSignals signals,
        DateTime now
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(request);
        var stats = config.Stats;
        var json = stats.Format is "json";
        if (request.Cache)
        {
            using var store = Open(config);
            var health = CacheHealth.Report(store);
            return json ? CacheHealth.Json(health) + "\n" : CacheHealth.Table(health);
        }
        if (Catalogue.IndexOf(stats.Plugin) >= 0)
        {
            using var store = Open(config);
            return PluginPage.Json(store, stats.Plugin) + "\n";
        }
        var report = Report(config, signals, now);
        if (request.SaveBaseline is { } save)
        {
            return Baseline.Save(home, save, StatsJson.Render(report)) + "\n";
        }
        var compare = request.Compare ?? (stats.Baseline.Trim() is { Length: > 0 } b ? b : null);
        return compare is not null ? Baseline.Compare(home, compare, StatsJson.Render(report))
            : json ? StatsJson.Render(report) + "\n"
            : StatsTable.Render(report);
    }

    /// <summary>
    /// A <c>since</c> window; a bad value is blamed on <paramref name="source"/>, the flag or
    /// the config key it came from.
    /// </summary>
    public static TimeSpan ParseSince(string text, string source)
    {
        try
        {
            return HumanDuration.ParseDaysOrHours(text);
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            throw new InvalidOperationException($"{source}: {e.Message}", e);
        }
    }

    private static StatsReport Report(RtokConfig config, StatsSignals signals, DateTime now)
    {
        var stats = config.Stats;
        var cutoff = now - ParseSince(stats.Since, "stats.since");
        var report = StatsCollector.Collect(
            stats.TranscriptsDir,
            cutoff,
            stats.Plugin,
            Replay.From(config),
            signals
        );
        RtokStore? store = null;
        try
        {
            store = Open(config);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // No store, no store rows: the transcript report stands on its own.
        }
        if (store is not null)
        {
            using (store)
            {
                StatsAttach.Api(report, store);
                StatsAttach.BashCmd(report, store, signals);
                StatsAttach.CheckpointNotes(report, store);
                if (stats.Price)
                {
                    StatsAttach.Costs(report, store, stats.Prices);
                }
            }
        }
        StatsAttach.Codex(report, stats.CodexDir, cutoff);
        return report;
    }

    private static RtokStore Open(RtokConfig config) =>
        RtokStore.Open(
            new StoreOptions(config.Core.DbPath) { ArchiveDir = config.Core.ArchiveDir }
        );
}
