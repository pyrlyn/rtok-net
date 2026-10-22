using System.Text.Json;

namespace Rtok.Proxy;

/// <summary>Where one wire's usage object sits and which keys it reads: the one usage walk
/// all four wires share.</summary>
/// <param name="Container">The object holding the counters (<c>usage</c>, Gemini's
/// <c>usageMetadata</c>).</param>
/// <param name="Input">The prompt counter.</param>
/// <param name="Output">The output counter.</param>
/// <param name="CacheRead">The cache-read counter.</param>
internal sealed record UsageFields(string Container, string Input, string Output, string CacheRead)
{
    /// <summary>A parent the container may sit under instead (Anthropic's <c>message</c>,
    /// Responses' <c>response</c>).</summary>
    public string? AltParent { get; init; }

    /// <summary>The cache-write counter; null on wires that report none.</summary>
    public string? CacheCreate { get; init; }

    /// <summary>The object inside the container that holds <see cref="CacheRead"/> (the OpenAI
    /// wires' <c>*_tokens_details</c>).</summary>
    public string? CacheReadDetails { get; init; }

    /// <summary>The prompt counter already contains the cached slice (OpenAI, Gemini), so it
    /// is subtracted to keep <see cref="Usage.Input"/> uncached-only.</summary>
    public bool InputIncludesCache { get; init; }

    /// <summary>A second counter billed as output (Gemini's <c>thoughtsTokenCount</c>).</summary>
    public string? OutputExtra { get; init; }

    /// <summary>The usage <paramref name="value"/> carries, or null without a usage object.</summary>
    /// <param name="value">A response body or one stream event.</param>
    /// <returns>The counters.</returns>
    public Usage? Find(JsonElement value)
    {
        var usage = Json.Get(value, Container);
        if (usage is null && AltParent is { } parent && Json.Get(value, parent) is { } nested)
        {
            usage = Json.Get(nested, Container);
        }
        if (usage is not { ValueKind: JsonValueKind.Object } found)
        {
            return null;
        }
        var cacheRead = CacheReadDetails is { } details
            ? Json.Get(found, details) is { } d
                ? Json.Int(d, CacheRead)
                : 0
            : Json.Int(found, CacheRead);
        var input = Json.Int(found, Input);
        if (InputIncludesCache)
        {
            input = Math.Max(0, Usage.Sub(input, cacheRead));
        }
        var extra = OutputExtra is { } e ? Json.Int(found, e) : 0;
        return new Usage(
            input,
            CacheCreate is { } create ? Json.Int(found, create) : 0,
            cacheRead,
            Usage.Add(Json.Int(found, Output), extra)
        );
    }
}
