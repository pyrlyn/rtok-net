namespace Rtok.Store;

/// <summary>One <c>usage</c> row as the prompt cache saw it: one API request.</summary>
public sealed record UsageTurn(
    long? CallId,
    string Api,
    long Input,
    long CacheCreate,
    long CacheRead
);
