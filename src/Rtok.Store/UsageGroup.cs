namespace Rtok.Store;

/// <summary>The <c>usage</c> counters of one group, summed.</summary>
/// <param name="Key">What the rows were grouped by: an api or a model id.</param>
/// <param name="Input">Uncached input tokens.</param>
/// <param name="CacheCreate">Tokens written to the prompt cache.</param>
/// <param name="CacheRead">Tokens read from the prompt cache.</param>
/// <param name="Output">Output tokens.</param>
public sealed record UsageGroup(
    string Key,
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output
);
