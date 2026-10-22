namespace Rtok.Report;

/// <summary>Ledger sums that stop at the edges of <see cref="long"/> instead of wrapping.</summary>
public static class Saturating
{
    /// <summary><paramref name="a"/> + <paramref name="b"/>, clamped.</summary>
    public static long Add(long a, long b)
    {
        var sum = unchecked(a + b);
        // Overflow only when both operands share a sign the sum does not.
        return ((a ^ sum) & (b ^ sum)) < 0 ? (a < 0 ? long.MinValue : long.MaxValue) : sum;
    }

    /// <summary><paramref name="a"/> − <paramref name="b"/>, clamped.</summary>
    public static long Sub(long a, long b) =>
        b == long.MinValue ? (a >= 0 ? long.MaxValue : a - b) : Add(a, -b);

    /// <summary>The values summed, clamped.</summary>
    public static long Sum(IEnumerable<long> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Aggregate(0L, Add);
    }

    /// <summary>A count as a chart coordinate: never above <see cref="int.MaxValue"/>.</summary>
    public static int ToInt(double value) =>
        double.IsNaN(value) ? 0
        : value >= int.MaxValue ? int.MaxValue
        : value <= int.MinValue ? int.MinValue
        : (int)value;
}
