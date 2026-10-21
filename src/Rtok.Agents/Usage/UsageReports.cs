using Rtok.Config;

namespace Rtok.Agents.Usage;

/// <summary>
/// <c>rtok agents usage</c>: tokens and estimated cost per agent, day and month, from the agents'
/// own logs, from what passed through rtok (the store's usage rows), or both. Costs come from
/// <c>[stats.prices]</c> through <see cref="UsageCost"/>; a model without a price counts in every
/// token total and is named in <see cref="UsageReport.Unpriced"/>, never guessed.
/// </summary>
public static class UsageReports
{
    /// <summary>The report <c>[agents.usage]</c> of <paramref name="config"/> describes.</summary>
    /// <param name="config">The loaded config: <c>[agents.usage]</c>, its dirs and <c>[stats]</c>.</param>
    /// <param name="ledger">rtok's store, read for <c>--source rtok</c> and <c>both</c> only.</param>
    /// <param name="now">Unix seconds that anchor a <c>since = "30d"</c> window.</param>
    /// <exception cref="ConfigException">A value of <c>[agents.usage]</c> is out of its range.</exception>
    public static UsageReport Build(RtokConfig config, IUsageLedger ledger, long now)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(ledger);
        var q = UsageQuery.Of(config.Agents.Usage, now);
        IReadOnlyList<UsageSkip> skipped = [];
        IReadOnlyList<UsageSlice> primary;
        if (q.Source is "rtok")
        {
            primary = ledger.Slices(q.Since, q.Until);
        }
        else
        {
            var logs = UsageLogReader.Read(config.Stats, config.Agents.Usage.Dirs, q.Since);
            primary = [.. logs.Slices.Where(s => s.Ts < q.Until)];
            skipped = [.. logs.Skipped.Where(s => q.Wants(s.Host))];
        }
        var through = q.Source is "logs"
            ? null
            : ThroughRtok.Of(q, ledger, q.Source is "rtok" ? primary : null, config.Stats.Prices);
        return Assemble(q, Tally.Of(q, primary, config.Stats.Prices), through, skipped);
    }

    /// <summary>The agent a request belongs to: its host id, else <c>unattributed (&lt;api&gt;)</c>.</summary>
    internal static string HostLabel(UsageSlice slice) =>
        slice.Host ?? $"unattributed ({slice.Api})";

    /// <summary>One row per agent, dearest first; with <paramref name="coverage"/>, what share went through rtok.</summary>
    private static List<UsageAgent> Agents(
        Tally tally,
        ThroughRtok? coverage,
        ThroughRtok? through
    ) =>
        tally
            .Agents.Select(kv =>
            {
                var row = kv.Value.Rounded();
                long? via = coverage?.Tokens.GetValueOrDefault(kv.Key);
                double? share =
                    via is { } t && row.Tokens > 0
                        ? Math.Round((double)t / row.Tokens * 1e4, MidpointRounding.AwayFromZero)
                            / 1e4
                        : null;
                var saved = through?.Saved(kv.Key, through.SavedByHost.GetValueOrDefault(kv.Key));
                return new UsageAgent(kv.Key, HostNames.Of(kv.Key), row, via, share, saved);
            })
            .OrderBy(a => a.Row, ByCost)
            .ToList();

    /// <summary>Every saving in the window, the unattributed one too; priced only where an agent's was.</summary>
    private static UsageSaved TotalSaved(ThroughRtok through, List<UsageAgent> agents) =>
        new(
            through.SavedByHost.Values.Sum() + through.SavedUnattributed,
            agents
                .Where(a => a.Saved?.Usd is not null)
                .Select(a => a.Saved!.Usd!.Value)
                .Aggregate((double?)null, (sum, x) => (sum ?? 0) + x)
                is { } usd
                ? UsageCost.Round6(usd)
                : null
        );

    private static UsageReport Assemble(
        UsageQuery q,
        Tally tally,
        ThroughRtok? through,
        IReadOnlyList<UsageSkip> skipped
    )
    {
        var both = q.Source is "both";
        if (both)
        {
            foreach (var host in through!.Tokens.Keys)
            {
                tally.Agents.TryAdd(host, new UsageRow());
            }
        }
        var agents = Agents(tally, both ? through : null, through);
        List<UsageModelRow> models =
        [
            .. tally
                .Models.Select(kv => new UsageModelRow(kv.Key, kv.Value.Rounded()))
                .OrderBy(m => m.Row, ByCost),
        ];
        var totalSaved = through is null ? null : TotalSaved(through, agents);
        return new UsageReport(
            q.Source,
            UsageZone.Name(q.Zone),
            tally.Days.Count == 0
                ? null
                : tally.Days.Select(d => d.Day).Max(StringComparer.Ordinal),
            new UsageTotals(
                tally.Totals.Rounded(),
                tally.Sessions.Count,
                tally.Days.Count,
                totalSaved
            ),
            tally.Unpriced.Keys.Select(k => k.Model).Distinct(StringComparer.Ordinal).Count(),
            [
                .. tally.Unpriced.Select(kv => new UsageUnpriced(
                    kv.Key.Model,
                    kv.Key.Host,
                    kv.Value
                )),
            ],
            agents,
            q.ByModel ? models : null,
            [.. tally.Periods.Select(kv => new UsagePeriod(kv.Key, kv.Value.Rounded()))],
            skipped,
            q.Daily
        );
    }

    /// <summary>
    /// Dearest first, then most tokens; a row with no priced model sorts below every priced one.
    /// The sort is stable, so ties keep the host or model order.
    /// </summary>
    private static readonly Comparer<UsageRow> ByCost = Comparer<UsageRow>.Create(
        (a, b) =>
        {
            var byCost = (b.CostUsd ?? -1).CompareTo(a.CostUsd ?? -1);
            return byCost != 0 ? byCost : b.Tokens.CompareTo(a.Tokens);
        }
    );
}
