namespace Rtok.Plugins.Graph;

/// <summary>A file the stat gate could not skip, with the sha the store holds for it.</summary>
internal sealed record ParseJob(
    string Path,
    string Rel,
    (long Mtime, long Size) Stat,
    string? Known,
    IReadOnlyDictionary<string, string> Extensions
);
