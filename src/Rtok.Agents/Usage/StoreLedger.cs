using Rtok.Store;

namespace Rtok.Agents.Usage;

/// <summary>The ledger as rtok's store keeps it: the proxy's <c>usage</c> rows and the <c>measurements</c>.</summary>
/// <param name="store">The open store.</param>
public sealed class StoreLedger(RtokStore store) : IUsageLedger
{
    /// <inheritdoc/>
    public IReadOnlyList<UsageSlice> Slices(long since, long until) =>
        [
            .. store
                .UsageSlices(since, until)
                .Select(r => new UsageSlice(
                    r.Host,
                    r.Api,
                    r.Session,
                    r.Model,
                    r.Ts,
                    r.Input,
                    r.CacheCreate,
                    r.CacheRead,
                    r.Output
                )),
        ];

    /// <inheritdoc/>
    public IReadOnlyList<(string? Host, long Saved)> SavedByHost(long since, long until) =>
        store.MeasurementSavedByHost(since, until);
}
