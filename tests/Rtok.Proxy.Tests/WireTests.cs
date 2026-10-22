using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rtok.Proxy.Tests;

public sealed class WireTests
{
    [Theory]
    [InlineData("/v1/messages", "anthropic")]
    [InlineData("/v1/chat/completions", "openai_chat")]
    [InlineData("/v1/responses", "openai_responses")]
    [InlineData("/v1beta/models/gemini-2.5-pro:generateContent", "gemini")]
    [InlineData("/v1beta/models/gemini-2.5-pro:streamGenerateContent", "gemini")]
    [InlineData("/v1/models", null)]
    [InlineData("/v1/messages/count_tokens", null)]
    public void Each_path_has_its_wire(string path, string? api) =>
        Assert.Equal(api, Wire.For(path)?.Api);

    [Fact]
    public void Anthropic_reads_usage_with_both_cache_counters()
    {
        var usage = Body(
            "/v1/messages",
            """{"usage":{"input_tokens":10,"cache_creation_input_tokens":3,"cache_read_input_tokens":5,"output_tokens":7}}"""
        );

        Assert.Equal(new Usage(10, 3, 5, 7), usage);
        Assert.Equal(25, usage!.Value.Total);
    }

    [Fact]
    public void Anthropic_stream_merges_message_start_and_delta()
    {
        var usage = Sse(
            "/v1/messages",
            """
            event: message_start
            data: {"type":"message_start","message":{"usage":{"input_tokens":10,"cache_read_input_tokens":5,"output_tokens":1}}}

            event: content_block_delta
            data: {"type":"content_block_delta","delta":{"text":"hi"}}

            event: message_delta
            data: {"type":"message_delta","usage":{"output_tokens":20}}

            """
        );

        Assert.Equal(new Usage(10, 0, 5, 20), usage);
    }

    [Fact]
    public void Openai_chat_takes_the_cached_slice_out_of_the_prompt()
    {
        var usage = Body(
            "/v1/chat/completions",
            """{"usage":{"prompt_tokens":100,"completion_tokens":9,"prompt_tokens_details":{"cached_tokens":60}}}"""
        );

        Assert.Equal(new Usage(40, 0, 60, 9), usage);
    }

    [Fact]
    public void Openai_chat_stream_ends_with_usage_before_done()
    {
        var usage = Sse(
            "/v1/chat/completions",
            """
            data: {"choices":[{"delta":{"content":"a"}}],"usage":null}

            data: {"choices":[],"usage":{"prompt_tokens":12,"completion_tokens":4.0}}

            data: [DONE]

            """
        );

        Assert.Equal(new Usage(12, 0, 0, 4), usage);
    }

    [Fact]
    public void Responses_counts_only_the_terminal_event()
    {
        var usage = Sse(
            "/v1/responses",
            """
            data: {"type":"response.created","response":{"usage":{"input_tokens":999,"output_tokens":999}}}

            data: {"type":"response.completed","response":{"usage":{"input_tokens":30,"output_tokens":6,"input_tokens_details":{"cached_tokens":10}}}}

            """
        );

        Assert.Equal(new Usage(20, 0, 10, 6), usage);
    }

    [Fact]
    public void Responses_body_reads_its_usage()
    {
        var usage = Body(
            "/v1/responses",
            """{"id":"r","usage":{"input_tokens":8,"output_tokens":2}}"""
        );

        Assert.Equal(new Usage(8, 0, 0, 2), usage);
    }

    [Fact]
    public void Gemini_bills_thoughts_as_output_and_takes_the_last_array_item()
    {
        var single = Body(
            "/v1beta/models/gemini-2.5-pro:generateContent",
            """{"usageMetadata":{"promptTokenCount":50,"cachedContentTokenCount":20,"candidatesTokenCount":5,"thoughtsTokenCount":7}}"""
        );
        var array = Body(
            "/v1beta/models/gemini-2.5-pro:streamGenerateContent",
            """[{"usageMetadata":{"promptTokenCount":1}},{"candidates":[]},{"usageMetadata":{"promptTokenCount":9,"candidatesTokenCount":3}},{"candidates":[]}]"""
        );

        Assert.Equal(new Usage(30, 0, 20, 12), single);
        Assert.Equal(new Usage(9, 0, 0, 3), array);
    }

    [Fact]
    public void Gemini_takes_the_model_from_the_path()
    {
        var wire = Wire.For("/v1beta/models/gemini-2.5-flash:generateContent")!;

        Assert.Equal(
            "gemini-2.5-flash",
            wire.Model("/v1beta/models/gemini-2.5-flash:generateContent", null)
        );
    }

    [Fact]
    public void A_body_without_usage_or_json_has_none()
    {
        Assert.Null(Body("/v1/messages", """{"id":"m"}"""));
        Assert.Null(Body("/v1/messages", "not json"));
        Assert.Null(Body("/v1/messages", """{"usage":"text"}"""));
    }

    [Fact]
    public void Counters_clamp_and_ignore_what_is_not_a_number()
    {
        var usage = Body(
            "/v1/messages",
            """{"usage":{"input_tokens":1e300,"output_tokens":"7","cache_read_input_tokens":5}}"""
        );

        Assert.Equal(new Usage(long.MaxValue, 0, 5, 0), usage);
        Assert.Equal(long.MaxValue, usage!.Value.Total);
    }

    [Fact]
    public void Sse_data_drops_empty_lines_and_done_and_survives_a_cut_character()
    {
        Assert.Equal(
            ["{\"a\":1}", "x"],
            UsageReader.SseData("event: e\ndata: {\"a\":1}\r\ndata:\ndata: [DONE]\ndata:x\n")
        );

        var cut = Encoding.UTF8.GetBytes(
            "data: {\"usage\":{\"output_tokens\":3}}\n\ndata: {\"t\":\"é"
        );
        var usage = UsageReader.FromResponse(
            Wire.For("/v1/messages")!,
            "text/event-stream",
            cut.AsMemory(0, cut.Length - 1)
        );

        Assert.Equal(new Usage(0, 0, 0, 3), usage);
    }

    [Theory]
    [InlineData("text/event-stream", true)]
    [InlineData("Text/Event-Stream; charset=utf-8", true)]
    [InlineData("application/json", false)]
    [InlineData(null, false)]
    public void Sse_is_known_by_its_content_type(string? contentType, bool sse) =>
        Assert.Equal(sse, UsageReader.IsSse(contentType));

    [Fact]
    public void Chat_streams_ask_for_usage_unless_the_caller_chose()
    {
        var wire = Wire.For("/v1/chat/completions")!;
        var stream = JsonNode.Parse("""{"stream":true}""")!.AsObject();
        var chosen = JsonNode
            .Parse("""{"stream":true,"stream_options":{"include_usage":false}}""")!
            .AsObject();
        var plain = JsonNode.Parse("""{"stream":false}""")!.AsObject();

        Assert.True(wire.PrepareRequest(stream, includeUsage: true));
        Assert.Equal(
            """{"stream":true,"stream_options":{"include_usage":true}}""",
            stream.ToJsonString()
        );
        Assert.False(wire.PrepareRequest(chosen, includeUsage: true));
        Assert.False(wire.PrepareRequest(plain, includeUsage: true));
        Assert.False(
            wire.PrepareRequest(
                JsonNode.Parse("""{"stream":true}""")!.AsObject(),
                includeUsage: false
            )
        );
    }

    [Fact]
    public void The_session_comes_from_the_wire_then_headers_then_the_body_hash()
    {
        var anthropic = Wire.For("/v1/messages");
        using var withUser = JsonDocument.Parse("""{"metadata":{"user_id":"u-1"}}""");
        using var bare = JsonDocument.Parse("""{"model":"m"}""");
        Func<string, string?> none = _ => null;

        Assert.Equal("u-1", ProxyRecorder.Session(anthropic, withUser.RootElement, none, []));
        Assert.Equal(
            "h-1",
            ProxyRecorder.Session(
                anthropic,
                bare.RootElement,
                h => string.Equals(h, "x-rtok-session", StringComparison.Ordinal) ? "h-1" : null,
                []
            )
        );
        Assert.Equal(
            ProxyRecorder.Session(anthropic, bare.RootElement, none, [1, 2]),
            ProxyRecorder.Session(null, null, none, [1, 2])
        );
    }

    [Fact]
    public void A_string_with_invalid_utf8_reads_as_absent()
    {
        byte[] raw = [.. "{\"user\":\"a"u8, 0x9B, .. "\",\"model\":\"mé"u8, 0xFF, .. "\"}"u8];
        using var doc = JsonDocument.Parse(raw);
        var wire = Wire.For("/v1/chat/completions");

        Assert.Equal(
            ProxyRecorder.Session(null, null, _ => null, raw),
            ProxyRecorder.Session(wire, doc.RootElement, _ => null, raw)
        );
        Assert.Null(ProxyRecorder.ModelOf(wire, "/v1/chat/completions", doc.RootElement));
    }

    private static Usage? Body(string path, string body) =>
        UsageReader.FromResponse(Wire.For(path)!, "application/json", Encoding.UTF8.GetBytes(body));

    private static Usage? Sse(string path, string body) =>
        UsageReader.FromResponse(
            Wire.For(path)!,
            "text/event-stream",
            Encoding.UTF8.GetBytes(body)
        );
}
