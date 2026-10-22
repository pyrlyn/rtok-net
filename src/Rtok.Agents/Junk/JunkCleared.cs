namespace Rtok.Agents.Junk;

/// <summary>What a <c>clear</c> run planned and, with <c>--yes</c>, freed.</summary>
/// <param name="Yes">Removal ran; otherwise a dry run.</param>
/// <param name="Items">Every item, planned or skipped.</param>
/// <param name="PlannedBytes">The planned items' bytes.</param>
/// <param name="FreedBytes">The removed items' bytes.</param>
public sealed record JunkCleared(
    bool Yes,
    IReadOnlyList<JunkPlanned> Items,
    long PlannedBytes,
    long FreedBytes
)
{
    /// <summary>Something planned was not removed: exit 1.</summary>
    public bool Failed => Items.Any(p => p.Failed);

    /// <summary>The sums of <paramref name="items"/>.</summary>
    public static JunkCleared Of(IReadOnlyList<JunkPlanned> items, bool yes)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new(
            yes,
            items,
            items.Where(p => p.Planned).Sum(p => p.Bytes),
            items.Where(p => yes && p.Clears).Sum(p => p.Bytes)
        );
    }
}
