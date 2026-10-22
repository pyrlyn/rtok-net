using System.Text.Json;
using System.Text.Json.Nodes;
using CsCheck;
using Rtok.Config;

namespace Rtok.Proxy.Tests;

public sealed class CacheKeyTests
{
    private const string Base = """
        {"model":"claude-x","max_tokens":5,"temperature":0.2,"system":"be brief",
         "tools":[{"name":"t","input_schema":{"type":"object"}}],
         "messages":[
           {"role":"user","content":[{"type":"text","text":"hi"},
             {"type":"image","source":{"type":"base64","data":"AAA"}}]},
           {"role":"assistant","content":[{"type":"tool_use","id":"u1","name":"t","input":{"a":1}}]},
           {"role":"user","content":[{"type":"tool_result","tool_use_id":"u1","content":"ok"}]}]}
        """;

    private static readonly SemanticCacheSection Cache = new();

    private static readonly string[] Params =
    [
        "\"a\":1",
        "\"b\":[2,{\"y\":1,\"x\":2}]",
        "\"c\":\"s\"",
        "\"d\":null",
    ];

    public static TheoryData<string, Action<JsonObject>> Included =>
        new()
        {
            { "extra param", b => b["top_k"] = 3 },
            { "param value", b => b["temperature"] = 0.3 },
            { "model", b => b["model"] = "claude-y" },
            { "system", b => b["system"] = "be long" },
            { "tool definition", b => Tool(b)["description"] = "new" },
            { "tool_use input", b => Block(b, 1, 0)["input"]!["a"] = 2 },
            { "image source", b => Block(b, 0, 1)["source"]!["data"] = "AAB" },
            { "typed digest", b => Block(b, 2, 0)["is_error"] = true },
            { "text", b => Block(b, 0, 0)["text"] = "hello" },
            { "openai tool_call_id", b => Message(b, 2)["tool_call_id"] = "c1" },
        };

    public static TheoryData<string, Action<JsonObject>> Excluded =>
        new()
        {
            { "metadata", b => b["metadata"] = new JsonObject { ["user_id"] = "x" } },
            { "user", b => b["user"] = "u" },
            { "stream false", b => b["stream"] = false },
            { "cache_control", b => Block(b, 0, 0)["cache_control"] = Ephemeral() },
            { "system cache_control", b => b["system"] = SystemBlocks(true) },
        };

    [Theory]
    [MemberData(nameof(Included))]
    public void Each_included_field_changes_the_key(string what, Action<JsonObject> change) =>
        Assert.False(Key(Base).AsSpan().SequenceEqual(Key(Changed(change))), what);

    [Theory]
    [MemberData(nameof(Excluded))]
    public void Excluded_fields_keep_the_key(string what, Action<JsonObject> change)
    {
        var before = string.Equals(what, "system cache_control", StringComparison.Ordinal)
            ? Changed(b => b["system"] = SystemBlocks(false))
            : Base;
        Assert.True(Key(before).AsSpan().SequenceEqual(Key(Changed(change))), what);
    }

    [Fact]
    public void Caller_method_and_upstream_scope_the_key()
    {
        var key = Key(Base);
        Assert.NotEqual(key, Key(Base, scope: Scope("x-api-key", "k2")));
        Assert.NotEqual(key, Key(Base, scope: Scope("anthropic-beta", "b")));
        Assert.NotEqual(key, Key(Base, scope: Scope(method: "PUT")));
        Assert.NotEqual(key, Key(Base, scope: Scope(target: "https://other/v1/messages")));
        Assert.Equal(key, Key(Base, scope: Scope("x-other", "v")));
    }

    [Fact]
    public void Caller_identity_separates_values_from_names()
    {
        string Identity(params (string Name, string[] Values)[] headers) =>
            CacheKey.CallerIdentity(n =>
                headers
                    .Where(h => string.Equals(h.Name, n, StringComparison.Ordinal))
                    .SelectMany(h => h.Values)
            );
        Assert.NotEqual(
            Identity(("x-api-key", ["ab"])),
            Identity(("x-api-key", ["a", "b"])),
            StringComparer.Ordinal
        );
        Assert.NotEqual(
            Identity(("x-api-key", ["a"])),
            Identity(("authorization", ["a"])),
            StringComparer.Ordinal
        );
        Assert.Equal(64, Identity().Length);
    }

    [Fact]
    public void Eligibility()
    {
        bool Eligible(string json, SemanticCacheSection? cache = null)
        {
            using var doc = JsonDocument.Parse(json);
            return CacheKey.Eligible(doc.RootElement, cache ?? Cache);
        }
        const string one = """{"messages":[{"role":"user","content":"a"}]}""";
        Assert.True(Eligible(one));
        Assert.False(Eligible("""{"stream":true,"messages":[{"role":"user","content":"a"}]}"""));
        Assert.False(Eligible("""{"input":"a"}"""), "Responses API");
        Assert.False(Eligible("""{"messages":[]}"""));
        Assert.False(Eligible(Base), "two user turns, tools");
        Assert.True(
            Eligible(Base, new SemanticCacheSection { MaxMessages = 2, RequireEmptyTools = false })
        );
        Assert.False(Eligible(Base, new SemanticCacheSection { MaxMessages = 2 }), "tools");
    }

    [Fact]
    public void Key_ignores_top_level_param_order_and_cache_control() =>
        Gen.Shuffle(Params)
            .Select(Gen.Bool)
            .Sample(t =>
            {
                var (parts, cached) = t;
                var control = cached ? ",\"cache_control\":{\"type\":\"ephemeral\"}" : "";
                var json =
                    $$"""{{{string.Join(',', parts)}},"model":"m","messages":[{"role":"user","content":[{"type":"text","text":"q"{{control}}}]}]}""";
                const string Sorted =
                    """{"a":1,"b":[2,{"x":2,"y":1}],"c":"s","d":null,"model":"m","messages":[{"role":"user","content":[{"type":"text","text":"q"}]}]}""";
                return Key(json).AsSpan().SequenceEqual(Key(Sorted));
            });

    [Fact]
    public void Two_api_keys_never_share_a_key() =>
        Gen
            .String.Select(Gen.String)
            .Where(t => !string.Equals(t.Item1, t.Item2, StringComparison.Ordinal))
            .Sample(t =>
                !Key(Base, scope: Scope("x-api-key", t.Item1))
                    .AsSpan()
                    .SequenceEqual(Key(Base, scope: Scope("x-api-key", t.Item2)))
            );

    private static byte[] Key(string json, string? scope = null)
    {
        using var doc = JsonDocument.Parse(json);
        return CacheKey.Of(AnthropicWire.Instance, doc.RootElement, Cache, scope ?? Scope())!;
    }

    private static string Scope(
        string? header = null,
        string? value = null,
        string method = "POST",
        string target = "https://api.anthropic.com/v1/messages"
    ) =>
        CacheKey.Scope(
            n => string.Equals(n, header, StringComparison.Ordinal) ? [value] : [],
            method,
            target
        );

    private static string Changed(Action<JsonObject> change)
    {
        var body = JsonNode.Parse(Base)!.AsObject();
        change(body);
        return body.ToJsonString();
    }

    private static JsonObject Message(JsonObject body, int i) => body["messages"]![i]!.AsObject();

    private static JsonObject Block(JsonObject body, int message, int block) =>
        Message(body, message)["content"]![block]!.AsObject();

    private static JsonObject Tool(JsonObject body) => body["tools"]![0]!.AsObject();

    private static JsonObject Ephemeral() => new() { ["type"] = "ephemeral" };

    private static JsonArray SystemBlocks(bool cached)
    {
        var block = new JsonObject { ["type"] = "text", ["text"] = "be brief" };
        if (cached)
        {
            block["cache_control"] = Ephemeral();
        }
        return [block];
    }
}
