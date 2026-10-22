namespace Rtok.Tasks;

/// <summary>
/// One project's tasks: the adapter that keeps them, the store that numbers and claims them.
/// Every <c>rtok task</c> command and MCP <c>task_*</c> tool goes through here.
/// </summary>
/// <param name="adapter">Where the tasks live.</param>
/// <param name="store">Counters, claims and agents.</param>
/// <param name="key">The project key (<see cref="Rtok.Config.ProjectKey"/>).</param>
/// <param name="prefix">The id prefix for new tasks.</param>
/// <param name="clock">The clock for staleness.</param>
public sealed class TaskProject(
    ITaskAdapter adapter,
    ITaskStore store,
    string key,
    string prefix,
    TimeProvider clock
)
{
    /// <summary>How often <see cref="CreateAsync"/> re-seeds and retries when an id turns out taken.</summary>
    public const int TakenRetries = 3;

    private readonly TaskAllocator _allocator = new(store);

    /// <summary>The adapter.</summary>
    public ITaskAdapter Adapter => adapter;

    /// <summary>The project key.</summary>
    public string Key => key;

    /// <summary>The id prefix for new tasks.</summary>
    public string Prefix => prefix;

    /// <summary>Creates a task with the next free id.</summary>
    /// <param name="task">The title, body and parent.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task.</returns>
    public async Task<PlanTask> CreateAsync(NewPlanTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (
            task.Parent is { } parent
            && await adapter.FindAsync(parent, ct).ConfigureAwait(false) is null
        )
        {
            throw new TaskException($"rtok task: no parent task {parent}");
        }
        for (var attempt = 1; ; attempt++)
        {
            // Tasks made by hand or by another machine raise the counter before it hands out an id.
            await SeedAsync(task.Parent, ct).ConfigureAwait(false);
            var id = await _allocator
                .AllocateAsync(key, prefix, task.Parent, ct)
                .ConfigureAwait(false);
            try
            {
                return await adapter.CreateAsync(id, task, ct).ConfigureAwait(false);
            }
            catch (TaskTakenException) when (attempt < TakenRetries) { }
        }
    }

    /// <summary>Raises the counter under <paramref name="parent"/> past every id the adapter holds, any prefix.</summary>
    /// <param name="parent">The parent, or null for top-level ids.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The counter afterwards.</returns>
    public async Task<uint> SeedAsync(TaskId? parent, CancellationToken ct = default)
    {
        var tasks = await adapter
            .ListAsync(new TaskFilter { All = true, Parent = parent }, ct)
            .ConfigureAwait(false);
        var depth = parent?.Depth ?? 0;
        var highest = tasks
            .Where(t => t.Id.Depth > depth)
            .Select(t => t.Id.Path[depth])
            .DefaultIfEmpty(0u)
            .Max();
        return await _allocator.SeedAsync(key, parent, highest, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Raises every counter to the highest number the adapter holds under it and reports what
    /// was off. Finished tasks count: their numbers were handed out. The adapter is only read.
    /// </summary>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The report.</returns>
    public async Task<SyncReport> SyncAsync(CancellationToken ct = default)
    {
        var tasks = await adapter
            .ListAsync(new TaskFilter { All = true }, ct)
            .ConfigureAwait(false);
        var duplicates = tasks
            .GroupBy(t => t.Id)
            .Where(g => g.Skip(1).Any())
            .Select(g => g.Key)
            .Order()
            .ToList();
        var raised = new List<SyncRaise>();
        var above = new List<TaskId>();
        // One counter per parent, the top level (null) first.
        foreach (var group in tasks.GroupBy(t => t.Id.Parent).OrderBy(g => g.Key))
        {
            var ids = group.Select(t => t.Id).Distinct().Order().ToList();
            var top = ids.Max(id => id.Path[^1]);
            var from = await _allocator.CounterAsync(key, group.Key, ct).ConfigureAwait(false);
            if (top > from)
            {
                await _allocator.SeedAsync(key, group.Key, top, ct).ConfigureAwait(false);
                above.AddRange(ids.Where(id => id.Path[^1] > from));
                raised.Add(new SyncRaise(group.Key, from, top));
            }
        }
        var counter = await _allocator.CounterAsync(key, null, ct).ConfigureAwait(false);
        var unlabelled = await adapter.UnlabelledAsync(ct).ConfigureAwait(false);
        return new SyncReport(
            adapter.Name,
            counter,
            TaskId.Of(prefix, counter + 1),
            raised,
            above,
            duplicates,
            unlabelled
        );
    }

    /// <summary>The last number handed out under a parent.</summary>
    /// <param name="parent">The parent, or null for top-level ids.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The counter.</returns>
    public Task<uint> CounterAsync(TaskId? parent, CancellationToken ct = default) =>
        _allocator.CounterAsync(key, parent, ct);

    /// <summary>The tasks the filter keeps.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tasks, by id.</returns>
    public Task<IReadOnlyList<PlanTask>> ListAsync(
        TaskFilter filter,
        CancellationToken ct = default
    ) => adapter.ListAsync(filter, ct);

    /// <summary>A task with its subtasks.</summary>
    /// <param name="id">The id.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task and the ids of its subtasks in any state.</returns>
    public async Task<TaskView> GetAsync(TaskId id, CancellationToken ct = default)
    {
        var task = await RequireAsync(id, ct).ConfigureAwait(false);
        var subtasks = await adapter
            .ListAsync(new TaskFilter { All = true, Parent = id }, ct)
            .ConfigureAwait(false);
        return new TaskView(task, [.. subtasks.Select(t => t.Id)]);
    }

    /// <summary>
    /// Reads a task, or moves it to <paramref name="state"/>. A parent cannot finish before its
    /// subtasks unless forced; finishing drops the claim.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="state">The new state, or null to read.</param>
    /// <param name="force">Finish a parent with open subtasks.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task.</returns>
    public async Task<PlanTask> StatusAsync(
        TaskId id,
        TaskState? state,
        bool force,
        CancellationToken ct = default
    )
    {
        if (state is not { } target)
        {
            return await RequireAsync(id, ct).ConfigureAwait(false);
        }
        if (!target.IsActive() && !force)
        {
            var open = await adapter
                .ListAsync(new TaskFilter { Parent = id }, ct)
                .ConfigureAwait(false);
            if (open.Count > 0)
            {
                throw new TaskException(
                    $"{id} has open subtasks: {string.Join(", ", open.Select(t => t.Id))}; finish them first or pass --force"
                );
            }
        }
        var task = await adapter.WriteStatusAsync(id, target, ct).ConfigureAwait(false);
        if (!target.IsActive())
        {
            await store.ClearClaimAsync(key, id, ct).ConfigureAwait(false);
        }
        return task;
    }

    /// <summary>The ready queue.</summary>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The claimable tasks, best first.</returns>
    public async Task<IReadOnlyList<ReadyItem>> ReadyAsync(CancellationToken ct = default)
    {
        using var held = await adapter.LockClaimsAsync(ct).ConfigureAwait(false);
        return await ReadyHeldAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The best ready task.</summary>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task, or null when nothing is ready.</returns>
    public async Task<PlanTask?> NextAsync(CancellationToken ct = default) =>
        (await ReadyAsync(ct).ConfigureAwait(false)) is [var first, ..] ? first.Task : null;

    /// <summary>
    /// Claims a task for an agent: the given one, or the first ready one that can be claimed.
    /// Re-claiming one's own task writes nothing but makes it the agent's latest claim.
    /// </summary>
    /// <param name="id">The task, or null for the next ready one.</param>
    /// <param name="agent">The full agent id.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task and whether it was written.</returns>
    public async Task<ClaimResult> ClaimAsync(
        TaskId? id,
        string agent,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(agent);
        using var held = await adapter.LockClaimsAsync(ct).ConfigureAwait(false);
        if (id is not null)
        {
            var task = await RequireAsync(id, ct).ConfigureAwait(false);
            return await ClaimHeldAsync(task, agent, ct).ConfigureAwait(false);
        }
        ClaimConflictException? last = null;
        foreach (var item in await ReadyHeldAsync(ct).ConfigureAwait(false))
        {
            try
            {
                return await ClaimHeldAsync(item.Task, agent, ct).ConfigureAwait(false);
            }
            catch (ClaimConflictException e)
            {
                last = e;
            }
        }
        throw last ?? new TaskException("no ready task");
    }

    /// <summary>Gives a claimed task back: no assignee, in-progress becomes open, the claim row goes.</summary>
    /// <param name="id">The task.</param>
    /// <param name="agent">The releasing agent, or null.</param>
    /// <param name="force">Release another agent's claim.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task.</returns>
    public async Task<PlanTask> ReleaseAsync(
        TaskId id,
        string? agent,
        bool force,
        CancellationToken ct = default
    )
    {
        using var held = await adapter.LockClaimsAsync(ct).ConfigureAwait(false);
        var task = await RequireAsync(id, ct).ConfigureAwait(false);
        if (!task.Status.IsActive())
        {
            throw new TaskException($"{id} is {task.Status.Wire()} and cannot be released");
        }
        if (
            task.Assignee is { } holder
            && !string.Equals(holder, agent, StringComparison.Ordinal)
            && !force
        )
        {
            throw new TaskException($"{id} is claimed by {holder}; pass --force");
        }
        var changed = task.Assignee is not null || task.Status == TaskState.InProgress;
        task.Assignee = null;
        task.Status = TaskState.Open;
        if (changed)
        {
            task = await adapter.SaveAsync(task, ct).ConfigureAwait(false);
        }
        await store.ClearClaimAsync(key, id, ct).ConfigureAwait(false);
        return task;
    }

    /// <summary>Makes <paramref name="id"/> wait for <paramref name="blocker"/>, unless that closes a cycle.</summary>
    /// <param name="id">The task that waits.</param>
    /// <param name="blocker">What it waits for.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task.</returns>
    public async Task<PlanTask> BlockAsync(
        TaskId id,
        TaskId blocker,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        if (id == blocker)
        {
            throw new TaskException($"{id} cannot depend on itself");
        }
        using var held = await adapter.LockClaimsAsync(ct).ConfigureAwait(false);
        var task = await RequireAsync(id, ct).ConfigureAwait(false);
        await RequireAsync(blocker, ct).ConfigureAwait(false);
        if (task.BlockedBy.Contains(blocker))
        {
            return task;
        }
        var all = await adapter.ListAsync(new TaskFilter { All = true }, ct).ConfigureAwait(false);
        if (Readiness.Cycle(all, id, blocker) is { } cycle)
        {
            throw new TaskException($"{id} would cycle: {string.Join(" -> ", cycle)}");
        }
        task.BlockedBy = [.. task.BlockedBy, blocker];
        return await adapter.SaveAsync(task, ct).ConfigureAwait(false);
    }

    /// <summary>Marks two tasks as not to run at the same time, on both sides, or removes the mark.</summary>
    /// <param name="id">One task.</param>
    /// <param name="other">The other.</param>
    /// <param name="remove">Remove the mark instead.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The first task.</returns>
    public async Task<PlanTask> ConflictAsync(
        TaskId id,
        TaskId other,
        bool remove,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        if (id == other)
        {
            throw new TaskException($"{id} cannot conflict with itself");
        }
        RequireMarkers();
        using var held = await adapter.LockClaimsAsync(ct).ConfigureAwait(false);
        var task = await RequireAsync(id, ct).ConfigureAwait(false);
        var peer = await adapter.FindAsync(other, ct).ConfigureAwait(false);
        if (peer is null && !remove)
        {
            throw new TaskException($"no task {other}");
        }
        // The peer first: a failure in between leaves the mark on the side that names it less.
        if (peer is not null && Mark(peer, id, remove))
        {
            await adapter.SaveAsync(peer, ct).ConfigureAwait(false);
        }
        return Mark(task, other, remove)
            ? await adapter.SaveAsync(task, ct).ConfigureAwait(false)
            : task;
    }

    /// <summary>Marks a task as safe beside any other, or clears the mark.</summary>
    /// <param name="id">The task.</param>
    /// <param name="on">Set or clear.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task.</returns>
    public async Task<PlanTask> SetParallelAsync(TaskId id, bool on, CancellationToken ct = default)
    {
        RequireMarkers();
        using var held = await adapter.LockClaimsAsync(ct).ConfigureAwait(false);
        var task = await RequireAsync(id, ct).ConfigureAwait(false);
        if (task.Parallel == on)
        {
            return task;
        }
        task.Parallel = on;
        return await adapter.SaveAsync(task, ct).ConfigureAwait(false);
    }

    /// <summary>Sets a task's priority, 0 (highest) to 4.</summary>
    /// <param name="id">The task.</param>
    /// <param name="level">The priority.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The task.</returns>
    public async Task<PlanTask> SetPriorityAsync(
        TaskId id,
        long level,
        CancellationToken ct = default
    )
    {
        if (level is < 0 or > PlanTask.MaxPriority)
        {
            throw new TaskException($"priority {level} is outside 0–{PlanTask.MaxPriority}");
        }
        using var held = await adapter.LockClaimsAsync(ct).ConfigureAwait(false);
        var task = await RequireAsync(id, ct).ConfigureAwait(false);
        if (task.Priority == level)
        {
            return task;
        }
        task.Priority = (int)level;
        return await adapter.SaveAsync(task, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The agent a claim binds to: <c>--agent</c>, else <c>RTOK_AGENT_ID</c>, resolved against
    /// the store's agents. An unknown flag is an error; an unknown variable is ignored with a warning.
    /// </summary>
    /// <param name="flag">The <c>--agent</c> value.</param>
    /// <param name="env">The <c>RTOK_AGENT_ID</c> value.</param>
    /// <param name="warn">Receives the warning about an unknown variable.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The full agent id, or null when neither names one.</returns>
    public async Task<string?> CallerAsync(
        string? flag,
        string? env,
        Action<string> warn,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(warn);
        if (!string.IsNullOrWhiteSpace(flag))
        {
            return await store.ResolveAgentAsync(flag.Trim(), ct).ConfigureAwait(false)
                ?? throw new TaskException($"--agent {flag.Trim()}: unknown");
        }
        if (!string.IsNullOrWhiteSpace(env))
        {
            var resolved = await store.ResolveAgentAsync(env.Trim(), ct).ConfigureAwait(false);
            if (resolved is null)
            {
                warn($"warning: RTOK_AGENT_ID {env.Trim()} is not a known agent; ignored");
            }
            return resolved;
        }
        return null;
    }

    /// <summary><see cref="CallerAsync"/> for commands that need an agent.</summary>
    /// <param name="flag">The <c>--agent</c> value.</param>
    /// <param name="env">The <c>RTOK_AGENT_ID</c> value.</param>
    /// <param name="warn">Receives warnings.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The full agent id.</returns>
    public async Task<string> RequireAgentAsync(
        string? flag,
        string? env,
        Action<string> warn,
        CancellationToken ct = default
    ) =>
        await CallerAsync(flag, env, warn, ct).ConfigureAwait(false)
        ?? throw new TaskException("no agent to bind: pass --agent or set RTOK_AGENT_ID");

    private async Task<PlanTask> RequireAsync(TaskId id, CancellationToken ct) =>
        await adapter.FindAsync(id, ct).ConfigureAwait(false)
        ?? throw new TaskException($"no task {id}");

    private void RequireMarkers()
    {
        if (!adapter.StoresMarkers)
        {
            throw new TaskException(
                $"{adapter.Name} tasks cannot store conflict or parallel markers yet: use the disk adapter"
            );
        }
    }

    private static bool Mark(PlanTask task, TaskId other, bool remove)
    {
        var has = task.ConflictsWith.Contains(other);
        if (has != remove)
        {
            return false;
        }
        task.ConflictsWith = remove
            ? [.. task.ConflictsWith.Where(c => c != other)]
            : [.. task.ConflictsWith, other];
        return true;
    }

    private async Task<IReadOnlyList<ReadyItem>> ReadyHeldAsync(CancellationToken ct)
    {
        var all = await adapter.ListAsync(new TaskFilter { All = true }, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (
            var holder in all.Select(t => t.Assignee)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
        )
        {
            seen[holder] = await IsStaleAsync(holder, now, ct).ConfigureAwait(false);
        }
        return Readiness.Select(all, holder => seen[holder]);
    }

    /// <summary>A store error counts as not stale: never take over a claim on a guess.</summary>
    private async Task<bool> IsStaleAsync(string holder, long now, CancellationToken ct)
    {
        try
        {
            return Readiness.IsStale(
                await store.AgentLastSeenAsync(holder, ct).ConfigureAwait(false),
                now
            );
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<ClaimResult> ClaimHeldAsync(
        PlanTask task,
        string agent,
        CancellationToken ct
    )
    {
        var id = task.Id;
        if (!task.Status.IsActive())
        {
            throw new ClaimConflictException($"{id} is {task.Status.Wire()} and cannot be claimed");
        }
        if (
            task.Status == TaskState.InProgress
            && string.Equals(task.Assignee, agent, StringComparison.Ordinal)
        )
        {
            await store.UpsertClaimAsync(key, id, agent, task.Title, ct).ConfigureAwait(false);
            return new ClaimResult(task, false);
        }
        foreach (var blocker in task.BlockedBy)
        {
            if (
                await adapter.FindAsync(blocker, ct).ConfigureAwait(false) is { } b
                && b.Status.IsActive()
            )
            {
                throw new ClaimConflictException($"{id} is blocked by {blocker}");
            }
        }
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (
            task.Assignee is { } holder
            && !string.Equals(holder, agent, StringComparison.Ordinal)
            && !await IsStaleAsync(holder, now, ct).ConfigureAwait(false)
        )
        {
            throw new ClaimConflictException($"{id} is claimed by {holder}");
        }
        task.Assignee = agent;
        task.Status = TaskState.InProgress;
        var saved = await adapter.SaveAsync(task, ct).ConfigureAwait(false);
        await store.UpsertClaimAsync(key, id, agent, saved.Title, ct).ConfigureAwait(false);
        return new ClaimResult(saved, true);
    }
}
