using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>The direct cache key of a request (catalog B9, rtok T298, T323, T533): a SHA-256
/// over everything that can change the answer, scoped by who asked, the method and the upstream
/// URL. Two requests share a key only when the provider would see the same question from the
/// same caller.</summary>
/// <remarks>The key covers the provider and model (each unless switched off), the system and
/// message text with tool calls and results spelled out, a typed digest of every message (block
/// types, <c>is_error</c>, names, image detail; <c>cache_control</c> dropped), the canonical tool
/// definitions, and every other top-level parameter key-sorted. Canonical JSON sorts object keys
/// ordinally at every depth, so key order never matters.</remarks>
internal static class CacheKey
{
    /// <summary>The headers that say who is asking; two callers never share an answer.</summary>
    internal static readonly string[] CallerHeaders =
    [
        "x-api-key",
        "authorization",
        "x-goog-api-key",
        "anthropic-version",
        "anthropic-beta",
        "openai-organization",
    ];

    /// <summary>Top-level fields the key reads elsewhere, or that never change the answer.</summary>
    private static readonly HashSet<string> NotParams = new(StringComparer.Ordinal)
    {
        "messages",
        "model",
        "system",
        "tools",
        "stream",
        "metadata",
        "user",
    };

    /// <summary>Whether a request may be answered from the cache at all: not a stream, one to
    /// <c>max_messages</c> user turns, and no tools when <c>require_empty_tools</c>. Responses
    /// API traffic carries <c>input</c>, not <c>messages</c>, so it is never eligible.</summary>
    public static bool Eligible(JsonElement body, SemanticCacheSection cache)
    {
        if (Json.Get(body, "stream") is { ValueKind: JsonValueKind.True })
        {
            return false;
        }
        var users = 0;
        if (Json.Get(body, "messages") is { ValueKind: JsonValueKind.Array } messages)
        {
            foreach (var message in messages.EnumerateArray())
            {
                if (
                    Json.Get(message, "role") is { ValueKind: JsonValueKind.String } role
                    && role.ValueEquals("user")
                )
                {
                    users++;
                }
            }
        }
        if (users == 0 || users > cache.MaxMessages)
        {
            return false;
        }
        return !cache.RequireEmptyTools
            || Json.Get(body, "tools") is not { ValueKind: JsonValueKind.Array } tools
            || tools.GetArrayLength() == 0;
    }

    /// <summary>SHA-256 over the caller headers: each name, then each of its values after a 0
    /// byte, then a 1 byte; hex.</summary>
    public static string CallerIdentity(Func<string, IEnumerable<string?>> values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var name in CallerHeaders)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(name));
            foreach (var value in values(name))
            {
                hash.AppendData([0]);
                hash.AppendData(Encoding.UTF8.GetBytes(value ?? ""));
            }
            hash.AppendData([1]);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>The scope a key is bound to: the caller, the method and the resolved upstream
    /// URL, so an answer cached for one lane's upstream is never served for another.</summary>
    public static string Scope(
        Func<string, IEnumerable<string?>> values,
        string method,
        string target
    )
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(CallerIdentity(values)));
        foreach (var part in new[] { method, target })
        {
            hash.AppendData([0]);
            hash.AppendData(Encoding.UTF8.GetBytes(part));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>The key, or null when the body names no string <c>model</c> or holds text that
    /// is not valid UTF-8.</summary>
    public static byte[]? Of(Wire wire, JsonElement body, SemanticCacheSection cache, string scope)
    {
        if (Json.Get(body, "model") is not { ValueKind: JsonValueKind.String } model)
        {
            return null;
        }
        try
        {
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer))
            {
                WritePrompt(json, wire, body, model.GetString()!, cache, scope);
            }
            return SHA256.HashData(buffer.ToArray());
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void WritePrompt(
        Utf8JsonWriter json,
        Wire wire,
        JsonElement body,
        string model,
        SemanticCacheSection cache,
        string scope
    )
    {
        json.WriteStartObject();
        json.WriteString("provider", cache.CacheByProvider ? wire.Provider : "");
        json.WriteString("model", cache.CacheByModel ? model : "");
        var system = Json.Get(body, "system");
        if (system is { } s)
        {
            json.WriteString("system", ContentText(s));
        }
        var messages = Messages(body);
        json.WriteStartArray("messages");
        foreach (var message in messages)
        {
            json.WriteStartArray();
            json.WriteStringValue(Json.String(message, "role") ?? "");
            json.WriteStringValue(MessageText(message));
            json.WriteEndArray();
        }
        json.WriteEndArray();
        json.WriteStartArray("typed");
        foreach (var message in messages)
        {
            json.WriteStartObject();
            json.WriteString("full", Digest(message, false));
            json.WriteString("shape", Digest(message, true));
            json.WriteEndObject();
        }
        json.WriteEndArray();
        if (system is { } st)
        {
            json.WriteString("system_typed", Digest(st, false));
        }
        if (ToolsFingerprint(body) is { } tools)
        {
            json.WriteString("tools_fingerprint", tools);
        }
        json.WritePropertyName("params");
        json.WriteStartObject();
        foreach (var p in Sorted(body).Where(p => !NotParams.Contains(p.Name)))
        {
            json.WritePropertyName(p.Name);
            Canonical(json, p.Value);
        }
        json.WriteEndObject();
        json.WriteString("caller", scope);
        json.WriteEndObject();
    }

    /// <summary>The messages that name a string role.</summary>
    private static List<JsonElement> Messages(JsonElement body) =>
        Json.Get(body, "messages") is { ValueKind: JsonValueKind.Array } messages
            ? [.. messages.EnumerateArray().Where(m => Json.String(m, "role") is not null)]
            : [];

    /// <summary>The message's text, with OpenAI's <c>tool_calls</c> and <c>tool_call_id</c>.</summary>
    private static string MessageText(JsonElement message)
    {
        var text = new StringBuilder(
            Json.Get(message, "content") is { } content ? ContentText(content) : ""
        );
        if (Json.Get(message, "tool_calls") is { } calls)
        {
            text.Append(" tool_calls ").Append(CanonicalText(calls));
        }
        if (Json.String(message, "tool_call_id") is { } id)
        {
            text.Append(" tool_call_id ").Append(id);
        }
        return text.ToString();
    }

    private static string ContentText(JsonElement content) =>
        content.ValueKind switch
        {
            JsonValueKind.String => content.GetString()!,
            JsonValueKind.Array => string.Join('\n', content.EnumerateArray().Select(BlockText)),
            _ => "",
        };

    /// <summary>One content block as text: whatever can change the answer is spelled out, and
    /// what is large (an image, a document) is hashed whole.</summary>
    private static string BlockText(JsonElement block)
    {
        if (Json.Get(block, "text") is { ValueKind: JsonValueKind.String } text)
        {
            return text.GetString()!;
        }
        if (block.ValueKind == JsonValueKind.String)
        {
            return block.GetString()!;
        }
        var kind = Json.String(block, "type") ?? "";
        return kind switch
        {
            "tool_result" =>
                $"tool_result {Json.String(block, "tool_use_id")}: {(Json.Get(block, "content") is { } c ? ContentText(c) : "")}",
            "tool_use" =>
                $"tool_use {Json.String(block, "id")} {Json.String(block, "name")} {CanonicalText(Json.Get(block, "input"))}",
            "image" or "document" =>
                $"{kind} {Hex(Encoding.UTF8.GetBytes(CanonicalText(Json.Get(block, "source"))))}",
            "image_url" =>
                $"image_url {Hex(Encoding.UTF8.GetBytes(Json.Get(block, "image_url") is { } u ? Json.String(u, "url") ?? "" : ""))}",
            _ => CanonicalText(block),
        };
    }

    /// <summary>SHA-256 of the canonical tool list, each tool followed by a 0 byte; the first
    /// eight hex digits. Null without tools.</summary>
    private static string? ToolsFingerprint(JsonElement body)
    {
        if (
            Json.Get(body, "tools") is not { ValueKind: JsonValueKind.Array } tools
            || tools.GetArrayLength() == 0
        )
        {
            return null;
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var tool in tools.EnumerateArray())
        {
            hash.AppendData(Encoding.UTF8.GetBytes(CanonicalText(tool)));
            hash.AppendData([0]);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset())[..8];
    }

    /// <summary>The message's canonical JSON hashed, <c>cache_control</c> dropped at every depth;
    /// <paramref name="blankText"/> empties string <c>text</c> and <c>content</c> so only the
    /// shape remains.</summary>
    private static string Digest(JsonElement value, bool blankText)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            Canonical(json, value, digest: true, blankText);
        }
        return Hex(buffer.ToArray());
    }

    private static string CanonicalText(JsonElement? value)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            if (value is { } v)
            {
                Canonical(json, v);
            }
            else
            {
                json.WriteNullValue();
            }
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>JSON with object keys sorted ordinally at every depth and strings re-encoded,
    /// so key order and escaping never change a key.</summary>
    internal static void Canonical(
        Utf8JsonWriter json,
        JsonElement value,
        bool digest = false,
        bool blankText = false
    )
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                json.WriteStartObject();
                foreach (var p in Sorted(value))
                {
                    if (digest && string.Equals(p.Name, "cache_control", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    json.WritePropertyName(p.Name);
                    if (
                        blankText
                        && p.Value.ValueKind == JsonValueKind.String
                        && p.Name is "text" or "content"
                    )
                    {
                        json.WriteStringValue("");
                    }
                    else
                    {
                        Canonical(json, p.Value, digest, blankText);
                    }
                }
                json.WriteEndObject();
                break;
            case JsonValueKind.Array:
                json.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    Canonical(json, item, digest, blankText);
                }
                json.WriteEndArray();
                break;
            case JsonValueKind.String:
                json.WriteStringValue(value.GetString());
                break;
            default:
                json.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
        }
    }

    private static IEnumerable<JsonProperty> Sorted(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object
            ? value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            : [];

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
