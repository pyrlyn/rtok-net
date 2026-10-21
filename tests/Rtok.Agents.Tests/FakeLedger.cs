using Rtok.Agents.Usage;

namespace Rtok.Agents.Tests;

/// <summary>A store's ledger held in memory, filtered by window the way the store filters it.</summary>
internal sealed class FakeLedger(
    IReadOnlyList<UsageSlice>? slices = null,
    IReadOnlyList<(string? Host, long Saved)>? saved = null
) : IUsageLedger
{
    public IReadOnlyList<UsageSlice> Slices(long since, long until) =>
        [.. (slices ?? []).Where(s => s.Ts >= since && s.Ts < until)];

    public IReadOnlyList<(string? Host, long Saved)> SavedByHost(long since, long until) =>
        saved ?? [];
}
