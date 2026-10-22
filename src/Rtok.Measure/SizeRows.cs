namespace Rtok.Measure;

/// <summary>
/// One table of size rows by name, with the bounded sample sidecar its p95 column needs.
/// <see cref="Finish"/> fills mean and p95 and drops the samples.
/// </summary>
internal sealed class SizeRows
{
    /// <summary>Samples kept per row: 2048 longs ≈ 16 KiB.</summary>
    public const int P95Cap = 2048;

    private readonly Dictionary<string, List<long>> _samples = new(StringComparer.Ordinal);

    public SortedDictionary<string, SizeRow> Rows { get; } = new(StringComparer.Ordinal);

    public void Add(string name, long bytes, long tokens, long ctt)
    {
        if (!Rows.TryGetValue(name, out var row))
        {
            row = new SizeRow();
            Rows[name] = row;
        }
        row.Count++;
        row.TotalBytes += bytes;
        row.Max = Math.Max(row.Max, bytes);
        row.EstTokens += tokens;
        row.Ctt = Sat(row.Ctt, ctt);
        Sample(name, bytes);
    }

    public void Sample(string name, long bytes)
    {
        if (!_samples.TryGetValue(name, out var v))
        {
            v = [];
            _samples[name] = v;
        }
        PushCapped(v, bytes);
    }

    /// <summary>Mean from totals; p95 nearest-rank over the samples, max when a row has none.</summary>
    public void Finish()
    {
        foreach (var (name, row) in Rows)
        {
            row.Mean = row.Count == 0 ? 0 : row.TotalBytes / row.Count;
            row.P95 = P95Of(name, row.Max);
        }
        _samples.Clear();
    }

    /// <summary>The p95 of <paramref name="name"/>'s samples, <paramref name="fallback"/> without any.</summary>
    public long P95Of(string name, long fallback) =>
        _samples.TryGetValue(name, out var v) && v.Count > 0 ? NearestP95(v) : fallback;

    /// <summary>
    /// Deterministic thinning once a row is full: keep the even-indexed half, then take the new
    /// sample. No RNG anywhere, so the same input gives the same p95.
    /// </summary>
    public static void PushCapped(List<long> v, long bytes)
    {
        if (v.Count >= P95Cap)
        {
            var kept = 0;
            for (var i = 0; i < v.Count; i += 2)
            {
                v[kept++] = v[i];
            }
            v.RemoveRange(kept, v.Count - kept);
        }
        v.Add(bytes);
    }

    /// <summary>Nearest-rank p95: the ⌈95n/100⌉-th smallest sample; sorts <paramref name="samples"/>.</summary>
    public static long NearestP95(List<long> samples)
    {
        if (samples.Count == 0)
        {
            return 0;
        }
        samples.Sort();
        var rank = (95 * samples.Count + 99) / 100;
        return samples[Math.Min(Math.Max(rank - 1, 0), samples.Count - 1)];
    }

    /// <summary>Saturating addition: a counter tops out instead of wrapping.</summary>
    public static long Sat(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;

    /// <summary>Saturating product of two non-negative counters.</summary>
    public static long SatMul(long a, long b) =>
        a == 0 || b == 0 ? 0
        : a > long.MaxValue / b ? long.MaxValue
        : a * b;
}
