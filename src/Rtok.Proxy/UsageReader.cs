using System.Text;
using System.Text.Json;

namespace Rtok.Proxy;

/// <summary>The usage a whole response reports, JSON or SSE, read through its wire.</summary>
public static class UsageReader
{
    /// <summary>Whether <paramref name="contentType"/> is an SSE stream.</summary>
    /// <param name="contentType">The response's <c>Content-Type</c>.</param>
    /// <returns>True for <c>text/event-stream</c>.</returns>
    public static bool IsSse(string? contentType) =>
        contentType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The response's counters. An SSE body is decoded per event with lossy UTF-8:
    /// the capture can end inside a multibyte character, and that loses only the event it cuts
    /// (rtok T584). The latest non-zero value of each counter wins.</summary>
    /// <param name="wire">The request's wire.</param>
    /// <param name="contentType">The response's <c>Content-Type</c>.</param>
    /// <param name="body">The captured response body.</param>
    /// <returns>The counters, or null when the response carries none.</returns>
    public static Usage? FromResponse(Wire wire, string? contentType, ReadOnlyMemory<byte> body)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (!IsSse(contentType))
        {
            using var doc = Json.Parse(body);
            return doc is null ? null : wire.UsageFromBody(doc.RootElement);
        }
        Usage? usage = null;
        foreach (var data in SseData(Encoding.UTF8.GetString(body.Span)))
        {
            using var doc = Json.Parse(Encoding.UTF8.GetBytes(data));
            if (doc is not null && wire.UsageFromSse(doc.RootElement) is { } next)
            {
                usage = (usage ?? default).Merge(next);
            }
        }
        return usage;
    }

    /// <summary>The tier the provider says it served the request on (rtok T385.12.2): OpenAI's
    /// <c>service_tier</c> on the response object (nested under <c>response</c> on a Responses
    /// stream), Anthropic's in <c>usage</c> (<c>message.usage</c> on <c>message_start</c>). It is
    /// the response's word, not the request's: a Flex request can come back on another tier. On
    /// a stream a later event can restate it, so the last one named wins.</summary>
    /// <param name="contentType">The response's <c>Content-Type</c>.</param>
    /// <param name="body">The captured response body.</param>
    /// <returns>The tier, or null when the response names none.</returns>
    public static string? ServiceTier(string? contentType, ReadOnlyMemory<byte> body)
    {
        if (!IsSse(contentType))
        {
            using var doc = Json.Parse(body);
            return doc is null ? null : Tier(doc.RootElement);
        }
        string? tier = null;
        foreach (var data in SseData(Encoding.UTF8.GetString(body.Span)))
        {
            using var doc = Json.Parse(Encoding.UTF8.GetBytes(data));
            tier = (doc is null ? null : Tier(doc.RootElement)) ?? tier;
        }
        return tier;
    }

    private static string? Tier(JsonElement value) =>
        Json.String(value, "service_tier")
        ?? (Json.Get(value, "usage") is { } usage ? Json.String(usage, "service_tier") : null)
        ?? (Json.Get(value, "response") is { } r ? Json.String(r, "service_tier") : null)
        ?? (
            Json.Get(value, "message") is { } m && Json.Get(m, "usage") is { } u
                ? Json.String(u, "service_tier")
                : null
        );

    /// <summary>The payloads of an SSE body's <c>data:</c> lines, without empty ones and
    /// <c>[DONE]</c>.</summary>
    internal static List<string> SseData(string text)
    {
        var found = new List<string>();
        foreach (var range in text.AsSpan().Split('\n'))
        {
            var line = text.AsSpan()[range];
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }
            var data = line[5..].Trim();
            if (data.Length > 0 && !data.SequenceEqual("[DONE]"))
            {
                found.Add(data.ToString());
            }
        }
        return found;
    }
}
