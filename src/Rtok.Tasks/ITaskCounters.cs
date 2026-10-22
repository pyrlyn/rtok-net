namespace Rtok.Tasks;

/// <summary>A project's counters inside <see cref="ITaskStore.WithCountersAsync{T}"/>.</summary>
public interface ITaskCounters
{
    /// <summary>The last number handed out under a parent key, 0 when none.</summary>
    /// <param name="parent">The parent's numbers (<c>2</c> for subtasks of <c>R2</c>), empty for top-level tasks.</param>
    /// <returns>The last number.</returns>
    uint Last(string parent);

    /// <summary>Stores the last number under a parent key.</summary>
    /// <param name="parent">The parent key.</param>
    /// <param name="last">The last number.</param>
    void SetLast(string parent, uint last);
}
