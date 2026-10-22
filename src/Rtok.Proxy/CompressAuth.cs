using Microsoft.AspNetCore.Http;

namespace Rtok.Proxy;

/// <summary>Credentials of the agent turn that archived a result, forwarded to the compressor call
/// so it bills the same account. Held in memory for one job; never stored or logged.</summary>
internal sealed class CompressAuth
{
    /// <summary>The credential and version headers an agent request lends the compressor.
    /// Everything else (session ids, tracing, the lane header) stays behind.</summary>
    private static readonly string[] Lent =
    [
        "x-api-key",
        "authorization",
        "anthropic-version",
        "anthropic-beta",
    ];

    private CompressAuth(IReadOnlyList<KeyValuePair<string, string>> headers) => Headers = headers;

    /// <summary>The headers the compressor request carries.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; }

    /// <summary>Null without a credential: the call would only fail.</summary>
    public static CompressAuth? Of(IReadOnlyList<KeyValuePair<string, string>> headers) =>
        headers.Any(h => IsCredential(h.Key)) ? new CompressAuth(headers) : null;

    /// <summary>The lent headers of an agent request. Null unless it came in on the Anthropic
    /// wire: the compressor speaks that wire only, so another provider's key would cross to the
    /// wrong destination.</summary>
    public static CompressAuth? FromHeaders(IHeaderDictionary headers, string? provider)
    {
        if (!string.Equals(provider, Wire.AnthropicProvider, StringComparison.Ordinal))
        {
            return null;
        }
        List<KeyValuePair<string, string>> lent = [];
        foreach (var name in Lent)
        {
            if (headers.TryGetValue(name, out var value) && value.ToString() is { Length: > 0 } v)
            {
                lent.Add(new(name, v));
            }
        }
        return Of(lent);
    }

    private static bool IsCredential(string name) =>
        name.Equals("x-api-key", StringComparison.OrdinalIgnoreCase)
        || name.Equals("authorization", StringComparison.OrdinalIgnoreCase);
}
