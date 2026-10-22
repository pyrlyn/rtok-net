using System.Text.Json;

namespace Rtok.Proxy;

/// <summary>Gemini <c>:generateContent</c> and <c>:streamGenerateContent</c>. The model is in the
/// path; <c>promptTokenCount</c> includes the cache and <c>thoughtsTokenCount</c> is billed as
/// output.</summary>
internal sealed class GeminiWire : Wire
{
    public static readonly GeminiWire Instance = new();

    private static readonly UsageFields Fields = new(
        "usageMetadata",
        "promptTokenCount",
        "candidatesTokenCount",
        "cachedContentTokenCount"
    )
    {
        InputIncludesCache = true,
        OutputExtra = "thoughtsTokenCount",
    };

    public override string Provider => GeminiProvider;

    public override string Api => "gemini";

    public override bool Matches(string path) =>
        path.EndsWith(":generateContent", StringComparison.Ordinal)
        || path.EndsWith(":streamGenerateContent", StringComparison.Ordinal);

    /// <summary>The body carries none; the model is the last path segment before <c>:</c>.</summary>
    public override string? Model(string path, JsonElement? body)
    {
        var last = path[(path.LastIndexOf('/') + 1)..];
        var colon = last.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 ? last[..colon] : null;
    }

    /// <summary>A non-SSE <c>streamGenerateContent</c> answers with an array; the totals ride on
    /// the last item that carries usage.</summary>
    public override Usage? UsageFromBody(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Array)
        {
            return Fields.Find(body);
        }
        Usage? last = null;
        foreach (var item in body.EnumerateArray())
        {
            last = Fields.Find(item) ?? last;
        }
        return last;
    }

    public override Usage? UsageFromSse(JsonElement sseEvent) => Fields.Find(sseEvent);
}
