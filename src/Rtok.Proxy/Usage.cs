using System.Runtime.InteropServices;

namespace Rtok.Proxy;

/// <summary>A provider's usage counters, an absent field read as zero. The four are disjoint:
/// <see cref="Input"/> is the uncached prompt only, whatever the wire.</summary>
/// <param name="Input">Uncached prompt tokens.</param>
/// <param name="CacheCreate">Prompt tokens written to the provider's cache.</param>
/// <param name="CacheRead">Prompt tokens read from the provider's cache.</param>
/// <param name="Output">Output tokens, thinking included.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Usage(long Input, long CacheCreate, long CacheRead, long Output)
{
    /// <summary>The billable total the provider <c>tokens</c> row records: the four counters
    /// add up because they are disjoint.</summary>
    public long Total => Add(Add(Add(Input, CacheCreate), CacheRead), Output);

    /// <summary>This usage with every counter <paramref name="next"/> carries (non-zero) taken
    /// from it: a stream restates its counters, and the latest wins.</summary>
    /// <param name="next">A later event's counters.</param>
    /// <returns>The merged counters.</returns>
    public Usage Merge(Usage next) =>
        new(
            next.Input != 0 ? next.Input : Input,
            next.CacheCreate != 0 ? next.CacheCreate : CacheCreate,
            next.CacheRead != 0 ? next.CacheRead : CacheRead,
            next.Output != 0 ? next.Output : Output
        );

    /// <summary><paramref name="a"/> + <paramref name="b"/>, held at the ends of the range
    /// instead of wrapping: a provider's counters are not trusted to be sane.</summary>
    internal static long Add(long a, long b)
    {
        var sum = unchecked(a + b);
        return ((a ^ sum) & (b ^ sum)) < 0 ? (a < 0 ? long.MinValue : long.MaxValue) : sum;
    }

    /// <summary><paramref name="a"/> − <paramref name="b"/>, held at the ends of the range.</summary>
    internal static long Sub(long a, long b)
    {
        var difference = unchecked(a - b);
        return ((a ^ b) & (a ^ difference)) < 0
            ? (a < 0 ? long.MinValue : long.MaxValue)
            : difference;
    }
}
