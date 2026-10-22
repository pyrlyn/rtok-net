namespace Rtok.Proxy;

/// <summary>A successful results download to read once it reached the client:
/// <paramref name="Scope"/> is the request target without its query, which holds the batch or
/// output-file id.</summary>
internal sealed record BatchFetch(BatchSource Source, string Scope);
