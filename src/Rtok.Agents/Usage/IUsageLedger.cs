namespace Rtok.Agents.Usage;

/// <summary>What passed through rtok, as its store recorded it: <c>--source rtok</c> and <c>both</c>.</summary>
public interface IUsageLedger
{
    /// <summary>The proxy's requests stamped in [<paramref name="since"/>, <paramref name="until"/>), unix seconds.</summary>
    IReadOnlyList<UsageSlice> Slices(long since, long until);

    /// <summary>
    /// Tokens rtok removed per host in the window (<c>est_before - est_after</c>, net of
    /// <c>expand</c>); a null host is a measurement no session claimed.
    /// </summary>
    IReadOnlyList<(string? Host, long Saved)> SavedByHost(long since, long until);
}
