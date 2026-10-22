using System.Text.Json;

namespace Rtok.Proxy;

/// <summary>Anthropic Messages, <c>/v1/messages</c>. The session is <c>metadata.user_id</c>;
/// <c>input_tokens</c> excludes the cache.</summary>
internal sealed class AnthropicWire : Wire
{
    public static readonly AnthropicWire Instance = new();

    private static readonly UsageFields Fields = new(
        "usage",
        "input_tokens",
        "output_tokens",
        "cache_read_input_tokens"
    )
    {
        AltParent = "message",
        CacheCreate = "cache_creation_input_tokens",
    };

    public override string Provider => AnthropicProvider;

    public override string Api => "anthropic";

    public override bool Matches(string path) =>
        string.Equals(path, "/v1/messages", StringComparison.Ordinal);

    public override string? SessionId(JsonElement body) =>
        Json.Get(body, "metadata") is { } metadata ? Json.String(metadata, "user_id") : null;

    public override Usage? UsageFromBody(JsonElement body) => Fields.Find(body);
}
