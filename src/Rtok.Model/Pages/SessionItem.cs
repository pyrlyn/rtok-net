namespace Rtok.Model;

/// <summary>One session with its usage, most recently active first on the page.</summary>
public sealed record SessionItem(
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
