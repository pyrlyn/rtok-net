namespace Rtok.Tasks;

/// <summary>
/// What tasks keep in rtok's store: the id counters, the claims and when agents were last
/// seen. The store (T8) implements it; <see cref="TaskAllocator"/> holds the counting rules.
/// </summary>
public interface ITaskStore
{
    /// <summary>
    /// Runs <paramref name="work"/> in one exclusive transaction over a project's counters, so
    /// processes never hand out the same number.
    /// </summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="project">The project key.</param>
    /// <param name="work">Reads and writes counters; its exceptions roll back.</param>
    /// <param name="ct">Cancels the wait for the lock.</param>
    /// <returns>What <paramref name="work"/> returned.</returns>
    Task<T> WithCountersAsync<T>(
        string project,
        Func<ITaskCounters, T> work,
        CancellationToken ct = default
    );

    /// <summary>Records <paramref name="agent"/>'s claim of a task as its latest one.</summary>
    /// <param name="project">The project key.</param>
    /// <param name="id">The task.</param>
    /// <param name="agent">The full agent id.</param>
    /// <param name="title">The task's title.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>When written.</returns>
    Task UpsertClaimAsync(
        string project,
        TaskId id,
        string agent,
        string title,
        CancellationToken ct = default
    );

    /// <summary>Drops a task's claim; a missing one is fine.</summary>
    /// <param name="project">The project key.</param>
    /// <param name="id">The task.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>When written.</returns>
    Task ClearClaimAsync(string project, TaskId id, CancellationToken ct = default);

    /// <summary>The claim <paramref name="agent"/> made last in a project.</summary>
    /// <param name="project">The project key.</param>
    /// <param name="agent">The full agent id.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The claim, or null.</returns>
    Task<TaskClaim?> LatestClaimAsync(string project, string agent, CancellationToken ct = default);

    /// <summary>When an agent was last seen, in Unix seconds.</summary>
    /// <param name="agent">The full agent id.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The time, or null when the store has no such agent.</returns>
    Task<long?> AgentLastSeenAsync(string agent, CancellationToken ct = default);

    /// <summary>The full id of a registered agent from its full or short id.</summary>
    /// <param name="agent">The id as typed.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The full id, or null when no agent matches.</returns>
    Task<string?> ResolveAgentAsync(string agent, CancellationToken ct = default);
}
