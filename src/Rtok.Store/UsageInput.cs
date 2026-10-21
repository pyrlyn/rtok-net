namespace Rtok.Store;

/// <summary>One <c>usage</c> row for <see cref="RtokStore.InsertUsageRows"/>: the four counters
/// plus the result's identity. A row with a key already stored is skipped.</summary>
public sealed record UsageInput(
    string? Model,
    string Api,
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output,
    string? ResultKey
);
