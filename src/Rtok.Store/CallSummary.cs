namespace Rtok.Store;

/// <summary>One call as the operator pages show it: the row, its attribution by slug and its
/// usage summed over the call's <c>usage</c> rows (null when it has none).</summary>
public sealed record CallSummary(
    long Id,
    long Ts,
    string Session,
    string Surface,
    string Kind,
    string? Plugin,
    string? Name,
    long? ParentId,
    double? Ms,
    bool Ok,
    string? Error,
    string? Host,
    string? Provider,
    string? Model,
    long? Input,
    long? CacheCreate,
    long? CacheRead,
    long? Output
);
