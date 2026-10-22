using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>A lane's <c>max_in_flight</c> cap (rtok T385.7): that many requests upstream at
/// once, up to <c>max_queued</c> more waiting for a slot, and the rest turned away with 429 so
/// the client backs off instead of piling up.</summary>
internal sealed class LaneGate : IDisposable
{
    /// <summary>The <c>Retry-After</c> a turned-away request gets: a slot frees in seconds.</summary>
    public const int RetryAfterSeconds = 1;

    private readonly SemaphoreSlim _slots;
    private readonly int _maxQueued;
    private int _waiting;

    private LaneGate(uint maxInFlight, uint maxQueued)
    {
        _slots = new SemaphoreSlim((int)Math.Min(maxInFlight, int.MaxValue));
        _maxQueued = (int)Math.Min(maxQueued, int.MaxValue);
    }

    /// <summary>Requests waiting for a slot right now.</summary>
    public int Waiting => Volatile.Read(ref _waiting);

    /// <summary>The gate a lane policy asks for; null when <c>max_in_flight</c> is 0.</summary>
    /// <param name="policy">The lane's policy.</param>
    /// <returns>The gate, or null.</returns>
    public static LaneGate? For(LanePolicy policy) =>
        policy.MaxInFlight > 0 ? new LaneGate(policy.MaxInFlight, policy.MaxQueued) : null;

    /// <summary>A slot, after waiting in line when all are taken; null when the line is full.
    /// The slot is held until it is disposed.</summary>
    /// <param name="cancel">The client went away.</param>
    /// <returns>The slot, or null.</returns>
    public async Task<IDisposable?> EnterAsync(CancellationToken cancel)
    {
        if (_slots.Wait(0, CancellationToken.None))
        {
            return new Slot(_slots);
        }
        if (Interlocked.Increment(ref _waiting) > _maxQueued)
        {
            Interlocked.Decrement(ref _waiting);
            return null;
        }
        try
        {
            await _slots.WaitAsync(cancel).ConfigureAwait(false);
            return new Slot(_slots);
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
        }
    }

    public void Dispose() => _slots.Dispose();

    private sealed class Slot(SemaphoreSlim slots) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                slots.Release();
            }
        }
    }
}
