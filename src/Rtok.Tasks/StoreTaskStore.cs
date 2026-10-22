using Rtok.Store;

namespace Rtok.Tasks;

/// <summary><see cref="ITaskStore"/> over rtok's store: <c>task_counters</c>, <c>task_claims</c>
/// and <c>agents</c>.</summary>
/// <param name="store">The open store; the caller disposes it.</param>
public sealed class StoreTaskStore(RtokStore store) : ITaskStore
{
    /// <inheritdoc/>
    public Task<T> WithCountersAsync<T>(
        string project,
        Func<ITaskCounters, T> work,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(store.WithTaskCounters(project, rows => work(new Counters(rows))));
    }

    /// <inheritdoc/>
    public Task UpsertClaimAsync(
        string project,
        TaskId id,
        string agent,
        string title,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        store.UpsertTaskClaim(project, id.ToString(), agent, title);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearClaimAsync(string project, TaskId id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        store.ClearTaskClaim(project, id.ToString());
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<TaskClaim?> LatestClaimAsync(
        string project,
        string agent,
        CancellationToken ct = default
    )
    {
        var row = store.LatestTaskClaim(project, agent);
        // A row whose id no longer parses is not a claim this build can name.
        return Task.FromResult(
            row is not null && TaskId.TryParse(row.TaskId, out var id)
                ? new TaskClaim(id, row.AgentId, row.Title, row.Since)
                : null
        );
    }

    /// <inheritdoc/>
    public Task<long?> AgentLastSeenAsync(string agent, CancellationToken ct = default) =>
        Task.FromResult(store.AgentLastSeen(agent));

    /// <inheritdoc/>
    public Task<string?> ResolveAgentAsync(string agent, CancellationToken ct = default) =>
        Task.FromResult(store.ResolveAgent(agent));

    private sealed class Counters(TaskCounters rows) : ITaskCounters
    {
        public uint Last(string parent) => (uint)rows.Last(parent);

        public void SetLast(string parent, uint last) => rows.SetLast(parent, last);
    }
}
