namespace Rtok.Tasks;

/// <summary>One counter <c>rtok task sync</c> found behind the adapter and raised.</summary>
/// <param name="Parent">The parent whose subtask counter this is; null for the top level.</param>
/// <param name="From">The counter before.</param>
/// <param name="To">The counter after: the highest number the adapter holds.</param>
public sealed record SyncRaise(TaskId? Parent, uint From, uint To);
