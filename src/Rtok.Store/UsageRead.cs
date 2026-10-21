namespace Rtok.Store;

/// <summary>
/// The <c>usage</c> rows of one request, summed, for <see cref="RtokStore.UsageSlices"/>: rows that
/// share api, session, model and second are one request.
/// </summary>
/// <param name="Host">The session's host slug, or null for a session with no host row.</param>
/// <param name="Api">The provider the request went to.</param>
/// <param name="Session">The session id.</param>
/// <param name="Model">The model id, or null.</param>
/// <param name="Ts">Unix seconds.</param>
/// <param name="Input">Uncached input tokens.</param>
/// <param name="CacheCreate">Tokens written to the prompt cache.</param>
/// <param name="CacheRead">Tokens read from the prompt cache.</param>
/// <param name="Output">Output tokens.</param>
public sealed record UsageRead(
    string? Host,
    string Api,
    string Session,
    string? Model,
    long Ts,
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output
);
