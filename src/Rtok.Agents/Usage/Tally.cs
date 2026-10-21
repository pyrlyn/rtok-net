using Rtok.Config;

namespace Rtok.Agents.Usage;

/// <summary>The requests of the window summed per agent, period and model, in key order.</summary>
internal sealed class Tally
{
    public UsageRow Totals { get; } = new();

    public SortedDictionary<string, UsageRow> Agents { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, UsageRow> Periods { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, UsageRow> Models { get; } = new(StringComparer.Ordinal);

    /// <summary>Tokens of the models without a price, per (model, agent).</summary>
    public SortedDictionary<(string Model, string Host), long> Unpriced { get; } =
        new(PairOrder.Instance);

    public HashSet<string> Sessions { get; } = new(StringComparer.Ordinal);

    public HashSet<(string Host, string Day)> Days { get; } = [];

    public static Tally Of(
        UsageQuery q,
        IEnumerable<UsageSlice> slices,
        IDictionary<string, ModelPrice> prices
    )
    {
        var t = new Tally();
        foreach (var s in slices)
        {
            var host = UsageReports.HostLabel(s);
            if (!q.Wants(host))
            {
                continue;
            }
            var model = s.Model ?? "unknown";
            double? cost = UsageCost.Price(prices, model) is { } p
                ? UsageCost.RowCost(s.Input, s.CacheCreate, s.CacheRead, s.Output, p)
                : null;
            var day = UsageZone.DayOf(s.Ts, q.Zone);
            if (cost is null)
            {
                t.Unpriced[(model, host)] = t.Unpriced.GetValueOrDefault((model, host)) + s.Tokens;
            }
            t.Totals.Add(s, cost);
            Row(t.Agents, host).Add(s, cost);
            Row(t.Periods, q.Daily ? day : day[..7]).Add(s, cost);
            Row(t.Models, model).Add(s, cost);
            t.Sessions.Add(s.Session);
            t.Days.Add((host, day));
        }
        return t;
    }

    private static UsageRow Row(SortedDictionary<string, UsageRow> rows, string key)
    {
        if (!rows.TryGetValue(key, out var row))
        {
            rows[key] = row = new UsageRow();
        }
        return row;
    }

    private sealed class PairOrder : IComparer<(string Model, string Host)>
    {
        public static readonly PairOrder Instance = new();

        public int Compare((string Model, string Host) x, (string Model, string Host) y)
        {
            var model = string.CompareOrdinal(x.Model, y.Model);
            return model != 0 ? model : string.CompareOrdinal(x.Host, y.Host);
        }
    }
}
