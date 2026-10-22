namespace Rtok.Tasks;

/// <summary>
/// Where a project keeps its tasks: Markdown files, GitHub issues or GitLab issues. Ids come
/// from the allocator, never from the adapter. Every failure is a <see cref="TaskException"/>.
/// </summary>
public interface ITaskAdapter
{
    /// <summary><c>disk</c>, <c>github</c> or <c>gitlab</c>.</summary>
    string Name { get; }

    /// <summary>Whether conflict and parallel markers can be stored.</summary>
    bool StoresMarkers { get; }

    /// <summary>Writes a new open task.</summary>
    /// <param name="id">The allocated id.</param>
    /// <param name="task">The title, body and parent.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task as stored.</returns>
    /// <exception cref="TaskTakenException">A task with <paramref name="id"/> exists.</exception>
    Task<PlanTask> CreateAsync(TaskId id, NewPlanTask task, CancellationToken ct = default);

    /// <summary>The task, in any state.</summary>
    /// <param name="id">The id.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task, or null.</returns>
    Task<PlanTask?> FindAsync(TaskId id, CancellationToken ct = default);

    /// <summary>The tasks the filter keeps, by id.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tasks.</returns>
    Task<IReadOnlyList<PlanTask>> ListAsync(TaskFilter filter, CancellationToken ct = default);

    /// <summary>Moves a task to <paramref name="state"/>; nothing else changes.</summary>
    /// <param name="id">The id.</param>
    /// <param name="state">The new state.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task as stored.</returns>
    Task<PlanTask> WriteStatusAsync(TaskId id, TaskState state, CancellationToken ct = default);

    /// <summary>Writes a task's state, assignee, priority and markers. The caller holds <see cref="LockClaimsAsync"/>.</summary>
    /// <param name="task">The task.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task as stored.</returns>
    Task<PlanTask> SaveAsync(PlanTask task, CancellationToken ct = default);

    /// <summary>Serializes claims across processes: held for a whole read-decide-write.</summary>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The lock; dispose to release.</returns>
    Task<IDisposable> LockClaimsAsync(CancellationToken ct = default);

    /// <summary>Issues labelled <c>rtok</c> that lost their id label; empty where that cannot happen.</summary>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The strays.</returns>
    Task<IReadOnlyList<StrayIssue>> UnlabelledAsync(CancellationToken ct = default);
}
