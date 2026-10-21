namespace Rtok.Model;

/// <summary>
/// Builds snapshots off the caller's thread. Refresh requests that arrive while a build runs
/// collapse into one following build; readers take the last complete snapshot and never wait
/// on a build. A build that throws keeps the previous snapshot and records the error.
/// </summary>
/// <typeparam name="T">The snapshot.</typeparam>
/// <param name="build">Builds one snapshot; runs on a pool thread, one at a time.</param>
/// <param name="built">Called on the build's thread after each new snapshot is published.</param>
public sealed class SnapshotBuilder<T>(Func<T> build, Action<T>? built = null)
    where T : class
{
    private const int Idle = 0;
    private const int Building = 1;
    private const int Pending = 2;

    private int _state;
    private T? _current;
    private string? _error;
    private long _builds;

    /// <summary>The last complete snapshot, or null before the first build ends.</summary>
    public T? Current => Volatile.Read(ref _current);

    /// <summary>The last build's error, or null when it succeeded.</summary>
    public string? Error => Volatile.Read(ref _error);

    /// <summary>Builds started so far, failed ones included.</summary>
    public long Builds => Interlocked.Read(ref _builds);

    /// <summary>True while a build runs or one is queued after it.</summary>
    public bool IsBuilding => Volatile.Read(ref _state) != Idle;

    /// <summary>Asks for a new snapshot and returns at once.</summary>
    public void Refresh()
    {
        while (true)
        {
            var state = Volatile.Read(ref _state);
            if (state == Pending)
            {
                return;
            }
            var next = state == Idle ? Building : Pending;
            if (Interlocked.CompareExchange(ref _state, next, state) == state)
            {
                if (state == Idle)
                {
                    _ = Task.Run(Run);
                }
                return;
            }
        }
    }

    private void Run()
    {
        while (true)
        {
            BuildOnce();
            // A request during the build queued one more; otherwise the worker stops. A request
            // landing between the two exchanges fails the second and loops into the first.
            if (Interlocked.CompareExchange(ref _state, Building, Pending) == Pending)
            {
                continue;
            }
            if (Interlocked.CompareExchange(ref _state, Idle, Building) == Building)
            {
                return;
            }
        }
    }

    private void BuildOnce()
    {
        Interlocked.Increment(ref _builds);
        T snapshot;
        try
        {
            snapshot = build();
        }
#pragma warning disable CA1031 // Fail open: a broken source must not stop the surface.
        catch (Exception e)
#pragma warning restore CA1031
        {
            Volatile.Write(ref _error, e.Message);
            return;
        }
        Volatile.Write(ref _error, null);
        Volatile.Write(ref _current, snapshot);
        built?.Invoke(snapshot);
    }
}
