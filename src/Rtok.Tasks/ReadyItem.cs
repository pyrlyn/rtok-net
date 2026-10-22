namespace Rtok.Tasks;

/// <summary>A task an agent can claim now.</summary>
/// <param name="Task">The task.</param>
/// <param name="Stale">Its holder has not been seen for half an hour, so the claim can be taken over.</param>
/// <param name="ConflictsInProgress">In-progress tasks it conflicts with, by id.</param>
public sealed record ReadyItem(
    PlanTask Task,
    bool Stale,
    IReadOnlyList<TaskId> ConflictsInProgress
);
