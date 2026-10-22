namespace Rtok.Tasks;

/// <summary>Which tasks an agent can claim now, and whether a new dependency would close a cycle.</summary>
public static class Readiness
{
    /// <summary>A holder unseen for longer than this, in seconds, loses its claim to the next agent.</summary>
    public const long StaleSeconds = 1800;

    /// <summary>
    /// The claimable tasks, by priority then id: open ones nobody holds, and any active one
    /// whose holder went stale. A task waits while a blocker is active. While no task has a
    /// blocker, a task with an active subtask waits for it too (leaves first).
    /// </summary>
    /// <param name="tasks">Every task, finished ones included.</param>
    /// <param name="isStale">Whether a holder is stale.</param>
    /// <returns>The ready tasks.</returns>
    public static IReadOnlyList<ReadyItem> Select(
        IReadOnlyList<PlanTask> tasks,
        Func<string, bool> isStale
    )
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(isStale);
        var active = tasks.Where(t => t.Status.IsActive()).ToList();
        var activeIds = active.Select(t => t.Id).ToHashSet();
        var leavesFirst = active.TrueForAll(t => t.BlockedBy.Count == 0);
        var parents = active.Where(t => t.Parent is not null).Select(t => t.Parent!).ToHashSet();
        var inProgress = active.Where(t => t.Status == TaskState.InProgress).ToList();
        var ready = new List<ReadyItem>();
        foreach (var task in active)
        {
            var stale = task.Assignee is { } holder && isStale(holder);
            var claimable = task.Status == TaskState.Open ? task.Assignee is null || stale : stale;
            if (
                !claimable
                || task.BlockedBy.Any(activeIds.Contains)
                || (leavesFirst && parents.Contains(task.Id))
            )
            {
                continue;
            }
            var conflicts = inProgress
                .Where(other =>
                    other.Id != task.Id
                    && (
                        task.ConflictsWith.Contains(other.Id)
                        || other.ConflictsWith.Contains(task.Id)
                    )
                )
                .Select(other => other.Id)
                .Order()
                .ToList();
            ready.Add(new ReadyItem(task, stale, conflicts));
        }
        return [.. ready.OrderBy(r => r.Task.Priority).ThenBy(r => r.Task.Id)];
    }

    /// <summary>Whether a holder last seen at <paramref name="lastSeen"/> is stale at <paramref name="now"/>.</summary>
    /// <param name="lastSeen">Unix seconds, or null when the store has no such agent.</param>
    /// <param name="now">Unix seconds.</param>
    /// <returns>Whether the claim can be taken over.</returns>
    public static bool IsStale(long? lastSeen, long now) =>
        lastSeen is not { } seen || now - seen > StaleSeconds;

    /// <summary>
    /// The cycle that <paramref name="task"/> waiting on <paramref name="blocker"/> would close,
    /// over blocker edges and parent-waits-for-subtask edges; rotated to start at the lowest id.
    /// </summary>
    /// <param name="tasks">Every task.</param>
    /// <param name="task">The task that would wait.</param>
    /// <param name="blocker">What it would wait for.</param>
    /// <returns>The cycle, or null.</returns>
    public static IReadOnlyList<TaskId>? Cycle(
        IReadOnlyList<PlanTask> tasks,
        TaskId task,
        TaskId blocker
    )
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var waits = new Dictionary<TaskId, List<TaskId>>();
        void Edge(TaskId from, TaskId to)
        {
            if (!waits.TryGetValue(from, out var list))
            {
                waits[from] = list = [];
            }
            list.Add(to);
        }
        foreach (var t in tasks)
        {
            foreach (var b in t.BlockedBy)
            {
                Edge(t.Id, b);
            }
            if (t.Parent is { } parent)
            {
                Edge(parent, t.Id);
            }
        }
        // A path blocker -> ... -> task closes the cycle task -> blocker -> ... -> task.
        var path = new List<TaskId> { task };
        var seen = new HashSet<TaskId> { task };
        bool Walk(TaskId at)
        {
            if (at == task)
            {
                return true;
            }
            if (!seen.Add(at))
            {
                return false;
            }
            path.Add(at);
            foreach (var next in waits.GetValueOrDefault(at) ?? [])
            {
                if (Walk(next))
                {
                    return true;
                }
            }
            path.RemoveAt(path.Count - 1);
            return false;
        }
        if (!Walk(blocker))
        {
            return null;
        }
        var start = path.IndexOf(path.Min()!);
        return [.. path.Skip(start), .. path.Take(start)];
    }
}
