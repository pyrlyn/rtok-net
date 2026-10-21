using Rtok.Config;

namespace Rtok.Agents.Usage;

/// <summary>
/// What passed through rtok, per host, for <c>--source rtok</c> and <c>both</c>: its tokens, what
/// rtok saved, and the input price those requests paid on average, the rate the saved tokens are
/// valued at (an estimate, labelled as one).
/// </summary>
internal sealed class ThroughRtok
{
    public Dictionary<string, long> Tokens { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, long> SavedByHost { get; } = new(StringComparer.Ordinal);

    /// <summary>Saved tokens no session claimed; counted only when the query keeps every host.</summary>
    public long SavedUnattributed { get; private set; }

    private Dictionary<string, (long Input, double Cost)> InputRate { get; } =
        new(StringComparer.Ordinal);

    /// <summary>The ledger's view of the window; <paramref name="slices"/> when the caller already read them.</summary>
    public static ThroughRtok Of(
        UsageQuery q,
        IUsageLedger ledger,
        IReadOnlyList<UsageSlice>? slices,
        IDictionary<string, ModelPrice> prices
    )
    {
        var t = new ThroughRtok();
        foreach (var s in slices ?? ledger.Slices(q.Since, q.Until))
        {
            var host = UsageReports.HostLabel(s);
            if (!q.Wants(host))
            {
                continue;
            }
            t.Tokens[host] = t.Tokens.GetValueOrDefault(host) + s.Tokens;
            if (UsageCost.Price(prices, s.Model ?? "unknown") is { } p)
            {
                var (input, cost) = t.InputRate.GetValueOrDefault(host);
                t.InputRate[host] = (input + s.Input, cost + s.Input * p.Input);
            }
        }
        foreach (var (host, saved) in ledger.SavedByHost(q.Since, q.Until))
        {
            if (host is null)
            {
                t.SavedUnattributed += q.Hosts.Count == 0 ? saved : 0;
            }
            else if (q.Wants(host))
            {
                t.SavedByHost[host] = t.SavedByHost.GetValueOrDefault(host) + saved;
            }
        }
        return t;
    }

    /// <summary><paramref name="tokens"/> saved for <paramref name="host"/>, valued at its average input price.</summary>
    public UsageSaved Saved(string host, long tokens) =>
        new(
            tokens,
            InputRate.TryGetValue(host, out var rate) && rate.Input > 0
                ? UsageCost.Round6(tokens * (rate.Cost / rate.Input) / 1e6)
                : null
        );
}
