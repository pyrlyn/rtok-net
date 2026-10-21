namespace Rtok.Store;

/// <summary>The four usage counters summed over every <c>usage</c> row, and how many rows there are.</summary>
public sealed record UsageTotals(
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output,
    long Rows
);
