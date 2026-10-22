using Rtok.Agents.Usage;
using Rtok.Config;
using Rtok.Store;
using static Rtok.Measure.StatsReport;

namespace Rtok.Measure;

/// <summary>The report rows that come from the store and other hosts' logs, not the transcripts.</summary>
internal static class StatsAttach
{
    /// <summary>The proxy's <c>usage</c> counters per api.</summary>
    public static void Api(StatsReport report, RtokStore store)
    {
        foreach (var t in store.UsageByApi())
        {
            report.Api[t.Key] = new ApiRow(t.Input, t.CacheCreate, t.CacheRead, t.Output);
        }
    }

    /// <summary>Counted sessions split by whether a <c>checkpoint:</c>/<c>session:</c> note names them.</summary>
    public static void CheckpointNotes(StatsReport report, RtokStore store)
    {
        var ids = store.CheckpointSessionIds().ToHashSet(StringComparer.Ordinal);
        report.Checkpoint = report.SessionStems.Count(ids.Contains);
        report.NoCheckpoint = Math.Max(report.Sessions - report.Checkpoint, 0);
    }

    /// <summary>Codex CLI sessions in the same window, as one more <c>api</c> row; none without a request.</summary>
    public static void Codex(StatsReport report, string dir, DateTime cutoff)
    {
        var since = new DateTimeOffset(
            DateTime.SpecifyKind(cutoff, DateTimeKind.Utc)
        ).ToUnixTimeSeconds();
        var slices = CodexLogs.Read(dir, since).Slices;
        if (slices.Count > 0)
        {
            report.Api["codex"] = new ApiRow(
                slices.Sum(s => s.Input),
                slices.Sum(s => s.CacheCreate),
                slices.Sum(s => s.CacheRead),
                slices.Sum(s => s.Output)
            );
        }
    }

    /// <summary>
    /// The <c>cmd</c> plugin's view: each Bash family's filter, unmarked bytes split by why, and
    /// rule savings on families still on the default rule. Without the plugin's filter kinds the
    /// families stay unlabelled and unranked; the unmarked split needs only the measurements.
    /// </summary>
    public static void BashCmd(StatsReport report, RtokStore store, StatsSignals signals)
    {
        var kind = signals.FilterKind;
        foreach (var name in report.BashFamilies.Rows.Keys.Where(_ => kind is not null))
        {
            report.BashFilter[name] = kind!(name);
        }
        var u = report.BashUnmarked;
        foreach (var m in store.MeasurementsOf("cmd"))
        {
            switch (m.Kind)
            {
                case "raw":
                    u.GateCalls++;
                    u.GateBytes += Math.Max(m.BeforeBytes, 0);
                    break;
                case "unmatched":
                    u.UnmatchedCalls++;
                    u.UnmatchedBytes += Math.Max(m.BeforeBytes, 0);
                    break;
                case "rule" when kind is not null:
                    var family = m.RefId?.Split(':')[0] ?? "";
                    if (family.Length > 0 && kind(family) is "default")
                    {
                        var bytes = Math.Max(m.AfterBytes, 0);
                        report.BashDefaultRule.Add(family, bytes, Calls.EstTokens(bytes), 0);
                    }
                    break;
            }
        }
        report.BashDefaultRule.Finish();
    }

    /// <summary>
    /// The store's per-model usage priced at <c>[stats.prices]</c> (<c>--price</c>). A model
    /// without a row lands in <c>unknown</c> and stays out of the totals.
    /// </summary>
    public static void Costs(
        StatsReport report,
        RtokStore store,
        IDictionary<string, ModelPrice> prices
    )
    {
        var costs = new CostReport();
        var (total, saved) = (0.0, 0.0);
        foreach (var t in store.UsageByModel())
        {
            var price = UsageCost.Price(prices, t.Key);
            (double Cost, double Saved)? priced = price is null
                ? null
                : RowCost(t.Input, t.CacheCreate, t.CacheRead, t.Output, price);
            if (priced is not { } p)
            {
                costs.Unknown.Add(t.Key);
            }
            else
            {
                (total, saved) = (total + p.Cost, saved + p.Saved);
            }
            costs.Models[t.Key] = new CostRow(
                t.Input,
                t.CacheCreate,
                t.CacheRead,
                t.Output,
                priced?.Cost,
                priced?.Saved
            );
        }
        costs.Unknown.Sort(StringComparer.Ordinal);
        costs.TotalCost = UsageCost.Round6(total);
        costs.TotalSaved = UsageCost.Round6(saved);
        report.Cost = costs;
    }

    /// <summary>
    /// USD for one model's counters, and what its cache reads saved against the uncached input
    /// price; each rounded to a millionth so goldens stay exact.
    /// </summary>
    public static (double Cost, double Saved) RowCost(
        long input,
        long cacheCreate,
        long cacheRead,
        long output,
        ModelPrice price
    ) =>
        (
            UsageCost.RowCost(input, cacheCreate, cacheRead, output, price),
            UsageCost.Round6(cacheRead / 1e6 * Math.Max(price.Input - price.CacheRead, 0))
        );
}
