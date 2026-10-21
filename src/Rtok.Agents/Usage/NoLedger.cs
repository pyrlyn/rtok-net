namespace Rtok.Agents.Usage;

/// <summary>The ledger for <c>--source logs</c>, which never reads one: empty, so no store is opened.</summary>
public sealed class NoLedger : IUsageLedger
{
    /// <summary>The one instance.</summary>
    public static readonly NoLedger Instance = new();

    private NoLedger() { }

    /// <inheritdoc/>
    public IReadOnlyList<UsageSlice> Slices(long since, long until) => [];

    /// <inheritdoc/>
    public IReadOnlyList<(string? Host, long Saved)> SavedByHost(long since, long until) => [];
}
