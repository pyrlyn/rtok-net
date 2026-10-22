using System.Threading.Channels;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// Background re-index of one root while <c>[plugins.graph] watch</c> is not <c>off</c>: the
/// one writer is a thread of <c>rtok mcp</c>, which starts it with <see cref="Start"/>. An
/// edit is in the index within a quiet window of the last event, so the next call reads
/// nothing; until then the stale banner names the waiting files.
/// </summary>
public static class GraphWatch
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    /// <summary>Watches <paramref name="root"/> until <paramref name="stop"/>, or returns
    /// <see langword="null"/> when watching is off or the root is refused; a root the OS
    /// cannot watch is logged and not watched.</summary>
    public static Thread? Start(Ctx cx, string root, CancellationToken stop)
    {
        ArgumentNullException.ThrowIfNull(cx);
        if (Indexer.Config(cx).Watch is "off" || GraphRoot.TryCheck(root) is not { } key)
        {
            return null;
        }

        var thread = new Thread(() => Run(cx, key, stop))
        {
            IsBackground = true,
            Name = "rtok graph watch",
        };
        thread.Start();
        return thread;
    }

    private static void Run(Ctx cx, string root, CancellationToken stop)
    {
        var events = Channel.CreateUnbounded<WatchEvent>(new() { SingleReader = true });
        try
        {
            using var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter =
                    NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            void Changed(object? _, FileSystemEventArgs e) =>
                events.Writer.TryWrite(new WatchEvent(e.FullPath));
            watcher.Changed += Changed;
            watcher.Created += Changed;
            watcher.Deleted += Changed;
            watcher.Renamed += (_, e) =>
            {
                events.Writer.TryWrite(new WatchEvent(e.OldFullPath));
                events.Writer.TryWrite(new WatchEvent(e.FullPath));
            };
            watcher.Error += (_, _) => events.Writer.TryWrite(new WatchEvent(null, Rescan: true));
            watcher.EnableRaisingEvents = true;
            Pump(cx, root, events.Reader, stop);
        }
        catch (Exception e)
            when (e is IOException or ArgumentException or PlatformNotSupportedException)
        {
            cx.Host.Log("error", "graph", "watch", $"watch {root}: {e.Message}");
        }
    }

    /// <summary>The debounce loop apart from the OS watcher, so tests feed it events. Ends on
    /// <paramref name="stop"/> or a completed channel, and never spins.</summary>
    internal static WatchState Pump(
        Ctx cx,
        string root,
        ChannelReader<WatchEvent> events,
        CancellationToken stop
    ) => Pump(new WatchState(cx, new Matcher(root, Indexer.Config(cx))), events, stop);

    /// <summary><see cref="Pump(Ctx, string, ChannelReader{WatchEvent}, CancellationToken)"/>
    /// on a state the caller holds.</summary>
    internal static WatchState Pump(
        WatchState state,
        ChannelReader<WatchEvent> events,
        CancellationToken stop
    )
    {
        // One outstanding wait at a time: a fresh one per tick would pile up while idle.
        Task<bool>? wait = null;
        while (!stop.IsCancellationRequested)
        {
            while (events.TryRead(out var ev))
            {
                state.Absorb(ev, DateTimeOffset.UtcNow);
            }

            state.Publish();
            state.Settle(DateTimeOffset.UtcNow);
            if (events.Completion.IsCompleted)
            {
                break;
            }

            wait ??= events.WaitToReadAsync(stop).AsTask();
            if (Task.WaitAny([wait], Tick) == 0)
            {
                wait = null;
            }
        }

        return state;
    }
}
