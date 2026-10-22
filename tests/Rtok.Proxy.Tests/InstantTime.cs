using System.Collections.Concurrent;

namespace Rtok.Proxy.Tests;

/// <summary>A clock whose timers fire at once and remember what they were asked to wait.</summary>
internal sealed class InstantTime : TimeProvider
{
    public ConcurrentQueue<TimeSpan> Waits { get; } = new();

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        Waits.Enqueue(dueTime);
        ThreadPool.QueueUserWorkItem(_ => callback(state));
        return new Timer();
    }

    private sealed class Timer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
