using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rtok.Proxy;

/// <summary>One provider API shape the proxy understands: which paths it owns, where its
/// session and model sit, and how its responses report usage (rtok T11.1).</summary>
public abstract class Wire
{
    /// <summary>Provider slug of the Anthropic Messages wire.</summary>
    public const string AnthropicProvider = "anthropic";

    /// <summary>Provider slug both OpenAI wires share.</summary>
    public const string OpenAiProvider = "openai";

    /// <summary>Provider slug of the Gemini wire.</summary>
    public const string GeminiProvider = "gemini";

    private static readonly Wire[] All =
    [
        AnthropicWire.Instance,
        OpenAiChatWire.Instance,
        OpenAiResponsesWire.Instance,
        GeminiWire.Instance,
    ];

    /// <summary>The provider slug for the <c>providers</c> row.</summary>
    public abstract string Provider { get; }

    /// <summary>The <c>usage.api</c> value: anthropic, openai_chat, openai_responses or gemini.</summary>
    public abstract string Api { get; }

    /// <summary>The wire that owns <paramref name="path"/>, or null for a path no wire knows
    /// (<c>/v1/models</c>, the batch and files APIs).</summary>
    /// <param name="path">The request path, still percent-encoded.</param>
    /// <returns>The wire.</returns>
    public static Wire? For(string path) => Array.Find(All, w => w.Matches(path));

    /// <summary>Whether this wire owns <paramref name="path"/>.</summary>
    /// <param name="path">The request path.</param>
    /// <returns>True when it does.</returns>
    public abstract bool Matches(string path);

    /// <summary>The provider's session identity in the request body; both OpenAI wires carry
    /// it in <c>user</c>.</summary>
    /// <param name="body">The parsed request body.</param>
    /// <returns>The session id, or null when the body names none.</returns>
    public virtual string? SessionId(JsonElement body) => Json.String(body, "user");

    /// <summary>The model the request names; the body's <c>model</c> on every wire but Gemini.</summary>
    /// <param name="path">The request path.</param>
    /// <param name="body">The parsed request body, when it is JSON.</param>
    /// <returns>The model slug.</returns>
    public virtual string? Model(string path, JsonElement? body) =>
        body is { } b ? Json.String(b, "model") : null;

    /// <summary>The usage a whole JSON response reports.</summary>
    /// <param name="body">The parsed response body.</param>
    /// <returns>The counters, or null without a usage object.</returns>
    public abstract Usage? UsageFromBody(JsonElement body);

    /// <summary>The usage one decoded SSE <c>data:</c> event reports.</summary>
    /// <param name="sseEvent">The event's JSON.</param>
    /// <returns>The counters, or null when the event carries none.</returns>
    public virtual Usage? UsageFromSse(JsonElement sseEvent) => UsageFromBody(sseEvent);

    /// <summary>The wire's own request shaping before forwarding (Chat's
    /// <c>stream_options</c>). Changes <paramref name="body"/> in place.</summary>
    /// <param name="body">The request body.</param>
    /// <param name="includeUsage"><c>[proxy] include_usage</c>.</param>
    /// <returns>Whether the body changed; only then is it written again.</returns>
    public virtual bool PrepareRequest(JsonObject body, bool includeUsage) => false;
}
