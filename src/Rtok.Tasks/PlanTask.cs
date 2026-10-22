namespace Rtok.Tasks;

/// <summary>
/// A task as every adapter holds it. Lists are kept sorted and without duplicates, and an
/// empty assignee is none (<see cref="Normalize"/>).
/// </summary>
public sealed class PlanTask
{
    /// <summary>The priority a task has unless set: the middle of 0 (highest) to 4.</summary>
    public const int DefaultPriority = 2;

    /// <summary>The lowest priority.</summary>
    public const int MaxPriority = 4;

    /// <summary>The id.</summary>
    public required TaskId Id { get; set; }

    /// <summary>The one-line title.</summary>
    public required string Title { get; set; }

    /// <summary>Why the task exists and what done means; may be empty.</summary>
    public string Description { get; set; } = "";

    /// <summary>The state.</summary>
    public TaskState Status { get; set; }

    /// <summary>The parent of a subtask.</summary>
    public TaskId? Parent { get; set; }

    /// <summary>Created, in Unix seconds.</summary>
    public long CreatedAt { get; set; }

    /// <summary>Last written, in Unix seconds.</summary>
    public long UpdatedAt { get; set; }

    /// <summary>The issue a remote adapter keeps the task in.</summary>
    public ExternalRef? External { get; set; }

    /// <summary>0 (highest) to 4.</summary>
    public int Priority { get; set; } = DefaultPriority;

    /// <summary>The full id of the agent that claimed the task.</summary>
    public string? Assignee { get; set; }

    /// <summary>Tasks that must finish first.</summary>
    public IReadOnlyList<TaskId> BlockedBy { get; set; } = [];

    /// <summary>Tasks that must not run at the same time; the marker is kept on both sides.</summary>
    public IReadOnlyList<TaskId> ConflictsWith { get; set; } = [];

    /// <summary>Safe to run beside any other task.</summary>
    public bool Parallel { get; set; }

    /// <summary>Sorts and dedupes the lists and turns a blank assignee into none.</summary>
    public void Normalize()
    {
        BlockedBy = [.. BlockedBy.Distinct().Order()];
        ConflictsWith = [.. ConflictsWith.Distinct().Order()];
        if (string.IsNullOrWhiteSpace(Assignee))
        {
            Assignee = null;
        }
    }

    /// <summary>A copy that shares nothing mutable with this one.</summary>
    /// <returns>The copy.</returns>
    public PlanTask Clone() =>
        new()
        {
            Id = Id,
            Title = Title,
            Description = Description,
            Status = Status,
            Parent = Parent,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            External = External,
            Priority = Priority,
            Assignee = Assignee,
            BlockedBy = [.. BlockedBy],
            ConflictsWith = [.. ConflictsWith],
            Parallel = Parallel,
        };
}
