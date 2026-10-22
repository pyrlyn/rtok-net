using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>OpenAI Flex on a lane that asks for it (rtok T385.5): <c>service_tier = "flex"</c>
/// is billed at Batch rates, and a Flex capacity miss answers 429 without charging.</summary>
/// <remarks>Source: OpenAI "Flex processing" (checked by rtok 2026-10-08). The guide offers two
/// ways out of a 429: retry with exponential backoff, or retry on standard processing. A
/// request that carried Flex because the client asked for it is the client's to retry: rtok
/// retries only when rtok set the tier. Anthropic and Gemini have no Flex tier.</remarks>
internal static class Flex
{
    /// <summary>Every retry holds the client's connection open, so the count has a ceiling
    /// whatever the config says.</summary>
    public const int MaxRetries = 5;

    /// <summary>The longest wait before a retry.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private const string Tier = "service_tier";

    /// <summary>The body with <c>service_tier = "flex"</c>, or null to forward it as it is: not
    /// an OpenAI request, not a JSON object, or the client chose a tier and <c>force</c> is off.
    /// A JSON <c>null</c> tier counts as unset.</summary>
    public static byte[]? Apply(string provider, byte[] body, FlexPolicy flex)
    {
        if (!string.Equals(provider, Wire.OpenAiProvider, StringComparison.Ordinal))
        {
            return null;
        }
        if (Json.ParseObject(body) is not { } json)
        {
            return null;
        }
        if (json.TryGetPropertyValue(Tier, out var chosen) && chosen is not null)
        {
            var isFlex =
                chosen is JsonValue v
                && v.TryGetValue<string>(out var name)
                && string.Equals(name, "flex", StringComparison.Ordinal);
            if (!flex.Force || isFlex)
            {
                return null;
            }
        }
        return SetTier(body, "flex");
    }

    /// <summary><paramref name="body"/> with <c>service_tier</c> set to
    /// <paramref name="tier"/>. Absent: spliced in after the opening brace, every other byte as
    /// the client sent it. Present: only a re-serialisation can replace the value.</summary>
    public static byte[]? SetTier(byte[] body, string tier)
    {
        if (Json.ParseObject(body) is not { } json)
        {
            return null;
        }
        if (json.ContainsKey(Tier))
        {
            json[Tier] = tier;
            return Json.Write(json);
        }
        var open = Array.FindIndex(body, b => b is not (0x20 or 0x09 or 0x0A or 0x0D));
        var field = Encoding.UTF8.GetBytes($"\"{Tier}\":\"{tier}\"" + (json.Count > 0 ? "," : ""));
        return [.. body.AsSpan(0, open + 1), .. field, .. body.AsSpan(open + 1)];
    }

    /// <summary><c>Retry-After</c> as delta-seconds; the HTTP-date form and anything else is
    /// ignored, and our own delay stands.</summary>
    public static TimeSpan? RetryAfter(HttpResponseHeaders headers)
    {
        if (!headers.NonValidated.TryGetValues("Retry-After", out var values))
        {
            return null;
        }
        var text = values.FirstOrDefault()?.Trim();
        return
            !string.IsNullOrEmpty(text)
            && text.All(char.IsAsciiDigit)
            && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var s)
            ? TimeSpan.FromSeconds(Math.Min(s, int.MaxValue))
            : null;
    }
}
