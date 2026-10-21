namespace Rtok.Store;

/// <summary>One session with its usage totals and the time of its last call (its start when it has none).</summary>
public sealed record SessionSummary(
    string Id,
    string? Host,
    string? Project,
    string? Cwd,
    string? Source,
    long StartedAt,
    long? EndedAt,
    long LastActivity,
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output
);
