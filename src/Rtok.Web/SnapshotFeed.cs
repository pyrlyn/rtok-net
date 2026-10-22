using Rtok.Web.Contracts;

namespace Rtok.Web;

/// <summary>The newest snapshot for every socket: each build replaces it, and a socket waits for
/// the next one without polling. Readers never wait on a build in progress.</summary>
internal sealed class SnapshotFeed
{
    private readonly Lock _lock = new();
    private TaskCompletionSource<(Snapshot Snapshot, long Version)> _next = New();
    private (Snapshot Snapshot, long Version)? _current;

    /// <summary>Takes a new snapshot; called on the build's thread.</summary>
    public void Publish(Snapshot snapshot)
    {
        TaskCompletionSource<(Snapshot, long)> waiting;
        (Snapshot, long) published;
        lock (_lock)
        {
            published = (snapshot, (_current?.Version ?? 0) + 1);
            _current = published;
            waiting = _next;
            _next = New();
        }
        waiting.TrySetResult(published);
    }

    /// <summary>A snapshot newer than <paramref name="seen"/>: the current one when it is, else
    /// the next to be published.</summary>
    /// <param name="seen">The version the caller has; 0 for none.</param>
    /// <returns>The snapshot and its version.</returns>
    public Task<(Snapshot Snapshot, long Version)> NextAsync(long seen)
    {
        lock (_lock)
        {
            return _current is { } current && current.Version > seen
                ? Task.FromResult(current)
                : _next.Task;
        }
    }

    private static TaskCompletionSource<(Snapshot, long)> New() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
