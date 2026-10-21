namespace Rtok.Model;

/// <summary>
/// Reads a slow page (Hosts, which probes every host) off the build: a build gets the
/// placeholder until the first background read lands, then the cached page. Once the cache is
/// older than <paramref name="ttl"/>, a build starts one more background read and keeps
/// returning the cached page meanwhile.
/// </summary>
/// <typeparam name="TPage">The page.</typeparam>
/// <param name="inner">The real source.</param>
/// <param name="placeholder">What a build gets before the first read lands.</param>
/// <param name="ttl">How long a read stays fresh.</param>
public sealed class BackgroundSource<TPage>(
    IPageSource<TPage> inner,
    TPage placeholder,
    TimeSpan ttl
) : IPageSource<TPage>
    where TPage : class
{
    private readonly Lock _lock = new();
    private (TPage Page, DateTimeOffset At)? _cached;
    private Task? _reading;
    private string? _error;

    /// <summary>The last background read's error, or null when it succeeded.</summary>
    public string? Error => Volatile.Read(ref _error);

    /// <summary>The background read in flight, if any; tests wait on it.</summary>
    internal Task Reading
    {
        get
        {
            lock (_lock)
            {
                return _reading ?? Task.CompletedTask;
            }
        }
    }

    /// <inheritdoc />
    public TPage Read(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_lock)
        {
            var stale = _cached is not var (_, at) || context.Time.GetUtcNow() - at >= ttl;
            if (stale && _reading is null)
            {
                _reading = Task.Run(() => Load(context));
            }
            return _cached?.Page ?? placeholder;
        }
    }

    private void Load(ModelContext context)
    {
        try
        {
            var page = inner.Read(context);
            lock (_lock)
            {
                _cached = (page, context.Time.GetUtcNow());
            }
            Volatile.Write(ref _error, null);
        }
#pragma warning disable CA1031 // Fail open: a failed read keeps the last page; the next stale build retries.
        catch (Exception e)
#pragma warning restore CA1031
        {
            Volatile.Write(ref _error, e.Message);
            return;
        }
        finally
        {
            lock (_lock)
            {
                _reading = null;
            }
        }
    }
}
