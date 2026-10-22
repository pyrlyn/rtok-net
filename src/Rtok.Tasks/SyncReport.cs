namespace Rtok.Tasks;

/// <summary>What <c>rtok task sync</c> found and raised.</summary>
/// <param name="Adapter">The adapter's name.</param>
/// <param name="Counter">The top-level counter afterwards; the next id is one past it.</param>
/// <param name="Next">The next top-level id.</param>
/// <param name="Raised">The counters that were behind.</param>
/// <param name="AboveCounter">
/// Ids numbered above their counter before the sync: made by hand, by another machine, or by
/// a checkout whose store this one never saw.
/// </param>
/// <param name="Duplicates">Ids more than one task holds; <c>create</c> refuses taken ids, so these came from outside.</param>
/// <param name="Unlabelled">Issues that kept rtok's label but lost their id label.</param>
public sealed record SyncReport(
    string Adapter,
    uint Counter,
    TaskId Next,
    IReadOnlyList<SyncRaise> Raised,
    IReadOnlyList<TaskId> AboveCounter,
    IReadOnlyList<TaskId> Duplicates,
    IReadOnlyList<StrayIssue> Unlabelled
)
{
    /// <summary>Nothing drifted: no id above its counter, no duplicate, no stray issue.</summary>
    public bool IsClean =>
        AboveCounter.Count == 0 && Duplicates.Count == 0 && Unlabelled.Count == 0;
}
