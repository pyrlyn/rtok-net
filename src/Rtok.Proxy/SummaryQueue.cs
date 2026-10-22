namespace Rtok.Proxy;

/// <summary>The compressor's background queue (rtok T385.9.2). The proxy hands over the archive ids
/// a request just rewrote and returns at once; each job runs on the thread pool and writes its
/// summary beside the archive row, so a request only ever reads what an earlier job left.</summary>
internal sealed class SummaryQueue : IDisposable
{
    /// <summary>Ids remembered as already tried; cleared when full so a long-lived proxy stays
    /// bounded.</summary>
    internal const int SeenCap = 10_000;

    private readonly Compressor _compressor;
    private readonly int _maxInFlight;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Lock _seenLock = new();
    private int _inFlight;

    public SummaryQueue(Compressor compressor, uint maxInFlight)
    {
        _compressor = compressor;
        _maxInFlight = (int)Math.Clamp(maxInFlight, 1U, int.MaxValue);
    }

    /// <summary>The compressor jobs running now.</summary>
    internal int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>Queues <paramref name="ids"/> and returns without waiting. An id is tried once
    /// per process; ids past the in-flight cap are not taken, so the next request that offers
    /// them starts them. Without a credential nothing starts: the extractive view stays.</summary>
    public void Submit(CompressAuth? auth, IEnumerable<string> ids)
    {
        if (auth is null)
        {
            return;
        }
        foreach (var id in ids)
        {
            var admit = Admit(id);
            if (admit is SummaryAdmit.Full)
            {
                break;
            }
            if (admit is SummaryAdmit.Run)
            {
                // Off the caller's thread: the agent request never waits on a compressor call.
                _ = Task.Run(() => RunAsync(auth, id));
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _compressor.Dispose();

    /// <summary>The slot is taken before the id is marked seen, in one compare-and-swap, so
    /// concurrent submits cannot both pass the cap and an id refused at the cap stays retryable.
    /// A duplicate hands its reservation straight back. <see cref="SummaryAdmit.Run"/> holds a
    /// slot until <see cref="Release"/>.</summary>
    internal SummaryAdmit Admit(string id)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref _inFlight);
            if (seen >= _maxInFlight)
            {
                return SummaryAdmit.Full;
            }
        } while (Interlocked.CompareExchange(ref _inFlight, seen + 1, seen) != seen);
        lock (_seenLock)
        {
            if (_seen.Count >= SeenCap)
            {
                _seen.Clear();
            }
            if (_seen.Add(id))
            {
                return SummaryAdmit.Run;
            }
        }
        Release();
        return SummaryAdmit.Seen;
    }

    /// <summary>Frees a slot <see cref="Admit"/> handed out.</summary>
    internal void Release() => Interlocked.Decrement(ref _inFlight);

    private async Task RunAsync(CompressAuth auth, string id)
    {
        try
        {
            // RunAsync warns about every failure itself and never throws.
            await _compressor.RunAsync(auth, id).ConfigureAwait(false);
        }
        finally
        {
            Release();
        }
    }
}
