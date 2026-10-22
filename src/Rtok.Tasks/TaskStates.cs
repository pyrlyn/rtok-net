using System.Text.Json.Serialization;

namespace Rtok.Tasks;

/// <summary>The words for <see cref="TaskState"/>.</summary>
public static class TaskStates
{
    /// <summary>Open or in progress: still in the plan.</summary>
    /// <param name="state">The state.</param>
    /// <returns>Whether the task is active.</returns>
    public static bool IsActive(this TaskState state) =>
        state is TaskState.Open or TaskState.InProgress;

    /// <summary>The wire word: <c>open</c>, <c>in-progress</c>, <c>done</c>, <c>closed</c>.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The word.</returns>
    public static string Wire(this TaskState state) =>
        state switch
        {
            TaskState.Open => "open",
            TaskState.InProgress => "in-progress",
            TaskState.Done => "done",
            _ => "closed",
        };

    /// <summary>
    /// Parses a state, case- and <c>_</c>-insensitive, with the aliases <c>todo</c>,
    /// <c>pending</c>, <c>completed</c>, <c>wontdo</c>, <c>won't-do</c> and <c>not-planned</c>.
    /// </summary>
    /// <param name="text">The word as typed.</param>
    /// <returns>The state.</returns>
    /// <exception cref="TaskException">The word is not a state.</exception>
    public static TaskState Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var word = text.Trim().ToLowerInvariant().Replace('_', '-');
        return word switch
        {
            "open" or "todo" or "pending" => TaskState.Open,
            "in-progress" => TaskState.InProgress,
            "done" or "completed" => TaskState.Done,
            "closed" or "wontdo" or "won't-do" or "not-planned" => TaskState.Closed,
            _ => throw new TaskException(
                $"unknown task status \"{word}\": use open, in-progress, done or closed"
            ),
        };
    }
}
