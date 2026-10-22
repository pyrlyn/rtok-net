namespace Rtok.Tasks;

/// <summary>
/// Hands out task ids per project and parent. The prefix is not part of the counter, so a
/// new prefix continues the numbering, and a number once handed out is never handed out again.
/// </summary>
/// <param name="store">The counters.</param>
public sealed class TaskAllocator(ITaskStore store)
{
    /// <summary>The next id: <c>R3</c> at the top level, <c>R2.4</c> under <c>R2</c>.</summary>
    /// <param name="project">The project key.</param>
    /// <param name="prefix">The prefix, any case.</param>
    /// <param name="parent">The parent, for a subtask.</param>
    /// <param name="ct">Cancels the wait for the store.</param>
    /// <returns>The id.</returns>
    /// <exception cref="TaskException">The prefix is bad, the parent is a subtask, or the parent was never handed out.</exception>
    public Task<TaskId> AllocateAsync(
        string project,
        string prefix,
        TaskId? parent,
        CancellationToken ct = default
    )
    {
        // Refusals come before the transaction, so they burn no number.
        var upper = TaskId.CheckPrefix(prefix);
        parent?.Child(1);
        return store.WithCountersAsync(
            project,
            counters =>
            {
                if (parent is not null)
                {
                    var top = counters.Last("");
                    if (parent.Path[0] > top)
                    {
                        throw new TaskException(
                            $"parent {parent} was never allocated in this project (last is {top})"
                        );
                    }
                }
                var key = Key(parent);
                var next = counters.Last(key) + 1;
                counters.SetLast(key, next);
                return parent?.Child(next) ?? TaskId.Of(upper, next);
            },
            ct
        );
    }

    /// <summary>The last number handed out under a parent, 0 when none.</summary>
    /// <param name="project">The project key.</param>
    /// <param name="parent">The parent, or null for top-level tasks.</param>
    /// <param name="ct">Cancels the wait for the store.</param>
    /// <returns>The last number.</returns>
    public Task<uint> CounterAsync(
        string project,
        TaskId? parent,
        CancellationToken ct = default
    ) => store.WithCountersAsync(project, counters => counters.Last(Key(parent)), ct);

    /// <summary>Raises a counter to at least <paramref name="atLeast"/>; it never goes down.</summary>
    /// <param name="project">The project key.</param>
    /// <param name="parent">The parent, or null for top-level tasks.</param>
    /// <param name="atLeast">The highest number already used.</param>
    /// <param name="ct">Cancels the wait for the store.</param>
    /// <returns>The counter afterwards.</returns>
    public Task<uint> SeedAsync(
        string project,
        TaskId? parent,
        uint atLeast,
        CancellationToken ct = default
    ) =>
        store.WithCountersAsync(
            project,
            counters =>
            {
                var key = Key(parent);
                var last = Math.Max(counters.Last(key), atLeast);
                counters.SetLast(key, last);
                return last;
            },
            ct
        );

    /// <summary>The counter key of a parent: its numbers, empty at the top level.</summary>
    /// <param name="parent">The parent.</param>
    /// <returns>The key.</returns>
    public static string Key(TaskId? parent) => parent?.Numbers ?? "";
}
