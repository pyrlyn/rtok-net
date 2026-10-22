namespace Rtok.Tasks;

/// <summary>A task with the ids of its subtasks, in every state.</summary>
/// <param name="Task">The task.</param>
/// <param name="Subtasks">Its direct subtasks, by id.</param>
public sealed record TaskView(PlanTask Task, IReadOnlyList<TaskId> Subtasks);
