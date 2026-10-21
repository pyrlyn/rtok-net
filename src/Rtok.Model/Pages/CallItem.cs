namespace Rtok.Model;

/// <summary>One call, newest first on the page.</summary>
public sealed record CallItem(
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
