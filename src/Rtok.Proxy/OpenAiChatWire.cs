using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rtok.Proxy;

/// <summary>OpenAI Chat Completions, <c>/v1/chat/completions</c>. <c>prompt_tokens</c> includes
/// the cached slice; there is no cache-write counter.</summary>
internal sealed class OpenAiChatWire : Wire
{
    public static readonly OpenAiChatWire Instance = new();

    private static readonly UsageFields Fields = new(
        "usage",
        "prompt_tokens",
        "completion_tokens",
        "cached_tokens"
    )
    {
        CacheReadDetails = "prompt_tokens_details",
        InputIncludesCache = true,
    };

    public override string Provider => OpenAiProvider;

    public override string Api => "openai_chat";

    public override bool Matches(string path) =>
        string.Equals(path, "/v1/chat/completions", StringComparison.Ordinal);

    public override Usage? UsageFromBody(JsonElement body) => Fields.Find(body);

    /// <summary>A stream reports <c>usage</c> only when asked, so a streaming request that has
    /// not opted in gets <c>stream_options.include_usage = true</c>. A caller's own value, or a
    /// <c>stream_options</c> that is not an object, is never overwritten.</summary>
    public override bool PrepareRequest(JsonObject body, bool includeUsage)
    {
        if (
            !includeUsage
            || !body.TryGetPropertyValue("stream", out var stream)
            || stream?.GetValueKind() != JsonValueKind.True
        )
        {
            return false;
        }
        if (!body.TryGetPropertyValue("stream_options", out var options))
        {
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
            return true;
        }
        if (options is not JsonObject found || found.ContainsKey("include_usage"))
        {
            return false;
        }
        found["include_usage"] = true;
        return true;
    }
}
