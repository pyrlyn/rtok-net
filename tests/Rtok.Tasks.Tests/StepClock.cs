namespace Rtok.Tasks.Tests;

/// <summary>A clock that only moves when waited on: every delay is recorded, jumps the clock by
/// its length and finishes at once, so pacing and rate-limit waits are seen without waiting.</summary>
internal sealed class StepClock(long unixSeconds) : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

    public List<TimeSpan> Delays { get; } = [];

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        Delays.Add(dueTime);
        _now += dueTime;
        // Fired off the caller's stack: Task.Delay keeps the timer it is handed.
        ThreadPool.QueueUserWorkItem(_ => callback(state));
        return new Fired();
    }

    private sealed class Fired : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
