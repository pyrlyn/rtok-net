namespace Rtok.Tasks;

/// <summary>Which tasks a list returns. With no status and not <see cref="All"/>, only active ones.</summary>
public sealed record TaskFilter
{
    /// <summary>The states to keep; empty means active, or all with <see cref="All"/>.</summary>
    public IReadOnlyCollection<TaskState> Statuses { get; init; } = [];

    /// <summary>Every state.</summary>
    public bool All { get; init; }

    /// <summary>Only the direct subtasks of this task.</summary>
    public TaskId? Parent { get; init; }

    /// <summary>Whether done or closed tasks can match, so an adapter must read them too.</summary>
    public bool WantsFinished => All || Statuses.Any(s => !s.IsActive());

    /// <summary>Whether <paramref name="task"/> matches.</summary>
    /// <param name="task">The task.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(PlanTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (Parent is not null && task.Parent != Parent)
        {
            return false;
        }
        if (Statuses.Count > 0)
        {
            return Statuses.Contains(task.Status);
        }
        return All || task.Status.IsActive();
    }

    /// <summary>The matching tasks, by id.</summary>
    /// <param name="tasks">The tasks.</param>
    /// <returns>The matches, sorted.</returns>
    public IReadOnlyList<PlanTask> Select(IEnumerable<PlanTask> tasks) =>
        [.. tasks.Where(Matches).OrderBy(t => t.Id)];
}
