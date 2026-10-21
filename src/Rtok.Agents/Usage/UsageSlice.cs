namespace Rtok.Agents.Usage;

/// <summary>
/// One request's tokens as an agent's own log (or rtok's store) recorded it. The four legs never
/// overlap: <see cref="Input"/> is the uncached part, reasoning is inside <see cref="Output"/>.
/// </summary>
/// <param name="Host">The host id (<c>claude</c>), or null for a proxy request whose session has no host.</param>
/// <param name="Api">The provider the request went to (<c>anthropic</c>).</param>
/// <param name="Session">The host's session id.</param>
/// <param name="Model">The raw model id, as the log wrote it.</param>
/// <param name="Ts">Unix seconds of the request.</param>
/// <param name="Input">Uncached input tokens.</param>
/// <param name="CacheCreate">Tokens written to the prompt cache.</param>
/// <param name="CacheRead">Tokens read from the prompt cache.</param>
/// <param name="Output">Output tokens, reasoning included.</param>
public sealed record UsageSlice(
    string? Host,
    string Api,
    string Session,
    string? Model,
    long Ts,
    long Input,
    long CacheCreate,
    long CacheRead,
    long Output
)
{
    /// <summary>All four legs.</summary>
    public long Tokens => Input + CacheCreate + CacheRead + Output;
}
