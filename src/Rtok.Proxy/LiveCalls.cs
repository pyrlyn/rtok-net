namespace Rtok.Proxy;

/// <summary>The newest plain-mode requests, in memory only (rtok T5.2).</summary>
public sealed class LiveCalls
{
    /// <summary>How many the ring keeps.</summary>
    public const int Capacity = 120;

    private readonly LinkedList<LiveCall> _calls = new();
    private readonly Lock _gate = new();

    /// <summary>How many it holds now.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count;
            }
        }
    }

    /// <summary>Adds <paramref name="call"/> as the newest, dropping the oldest past the cap.</summary>
    /// <param name="call">The finished request.</param>
    public void Push(LiveCall call)
    {
        lock (_gate)
        {
            _calls.AddFirst(call);
            while (_calls.Count > Capacity)
            {
                _calls.RemoveLast();
            }
        }
    }

    /// <summary>The calls, newest first.</summary>
    /// <returns>A copy.</returns>
    public IReadOnlyList<LiveCall> Snapshot()
    {
        lock (_gate)
        {
            return [.. _calls];
        }
    }
}
