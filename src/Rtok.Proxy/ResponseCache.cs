namespace Rtok.Proxy;

/// <summary>The direct tier of rtok's semantic cache (T298, T323): whole 2xx answers by
/// <see cref="CacheKey"/>, in memory, for <c>ttl_s</c>. A cut or capped body is never stored, so
/// a hit always replays a complete answer.</summary>
internal sealed class ResponseCache(TimeProvider time)
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>The live answer stored under <paramref name="key"/>.</summary>
    public CachedAnswer? Lookup(byte[] key, TimeSpan ttl)
    {
        var id = Convert.ToHexStringLower(key);
        lock (_lock)
        {
            return _entries.TryGetValue(id, out var entry) && Alive(entry, ttl)
                ? entry.Answer
                : null;
        }
    }

    /// <summary>Keeps a 2xx answer; drops the expired ones on the way.</summary>
    public void Store(byte[] key, TimeSpan ttl, CachedAnswer answer)
    {
        if (answer.Status is < 200 or >= 300)
        {
            return;
        }
        var id = Convert.ToHexStringLower(key);
        lock (_lock)
        {
            foreach (var stale in _entries.Where(e => !Alive(e.Value, ttl)).ToList())
            {
                _entries.Remove(stale.Key);
            }
            _entries[id] = new Entry(answer, time.GetTimestamp());
        }
    }

    private bool Alive(Entry entry, TimeSpan ttl) => time.GetElapsedTime(entry.At) < ttl;

    private sealed record Entry(CachedAnswer Answer, long At);
}
