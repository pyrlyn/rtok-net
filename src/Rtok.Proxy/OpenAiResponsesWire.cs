using System.Text.Json;

namespace Rtok.Proxy;

/// <summary>OpenAI Responses, <c>/v1/responses</c>. A stream reports usage once, on its
/// terminal event, nested under <c>response</c>.</summary>
internal sealed class OpenAiResponsesWire : Wire
{
    public static readonly OpenAiResponsesWire Instance = new();

    private static readonly UsageFields Fields = new(
        "usage",
        "input_tokens",
        "output_tokens",
        "cached_tokens"
    )
    {
        AltParent = "response",
        CacheReadDetails = "input_tokens_details",
        InputIncludesCache = true,
    };

    public override string Provider => OpenAiProvider;

    public override string Api => "openai_responses";

    public override bool Matches(string path) =>
        string.Equals(path, "/v1/responses", StringComparison.Ordinal);

    public override Usage? UsageFromBody(JsonElement body) => Fields.Find(body);

    public override Usage? UsageFromSse(JsonElement sseEvent) =>
        Json.String(sseEvent, "type")
            is "response.completed"
                or "response.incomplete"
                or "response.failed"
            ? Fields.Find(sseEvent)
            : null;
}
