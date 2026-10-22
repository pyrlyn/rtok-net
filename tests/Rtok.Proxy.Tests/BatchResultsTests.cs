using System.Text;
using Microsoft.EntityFrameworkCore;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rtok.Proxy.Tests;

public sealed class BatchResultsTests
{
    private const string AnthropicLine = """
        {"custom_id":"a","result":{"type":"succeeded","message":{"model":"claude-sonnet-5-5","usage":{"input_tokens":10,"cache_creation_input_tokens":3,"cache_read_input_tokens":4,"output_tokens":5}}}}
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Anthropic_succeeded_lines_become_rows_and_the_rest_do_not()
    {
        var body = Jsonl(
            AnthropicLine,
            """{"custom_id":"b","result":{"type":"errored","error":{"type":"error"}}}""",
            """{"custom_id":"c","result":{"type":"expired"}}""",
            """{"custom_id":"d","result":{"type":"canceled"}}"""
        );

        var row = Assert.Single(BatchResults.Rows(body, "s", null));

        Assert.Equal(
            ("claude-sonnet-5-5", "anthropic", 10L, 3L, 4L, 5L),
            (row.Model, row.Api, row.Input, row.CacheCreate, row.CacheRead, row.Output)
        );
        Assert.Equal(BatchResults.Key("anthropic", "s", "a"), row.ResultKey);
    }

    [Fact]
    public void Openai_lines_pick_their_wire_from_the_body_and_skip_failures()
    {
        var body = Jsonl(
            """{"id":"r1","custom_id":"a","error":null,"response":{"status_code":200,"body":{"object":"chat.completion","model":"gpt-5","usage":{"prompt_tokens":100,"completion_tokens":7,"prompt_tokens_details":{"cached_tokens":40}}}}}""",
            """{"id":"r2","custom_id":"b","error":null,"response":{"status_code":200,"body":{"object":"response","model":"gpt-5","usage":{"input_tokens":9,"output_tokens":2}}}}""",
            """{"id":"r3","custom_id":"c","error":null,"response":{"status_code":429,"body":{"usage":{"prompt_tokens":1,"completion_tokens":1}}}}""",
            """{"id":"r4","custom_id":"d","error":{"code":"batch_expired"},"response":null}"""
        );

        var rows = BatchResults.Rows(body, "s", "fallback");

        Assert.Equal(
            [("openai_chat", 60L, 40L, 7L), ("openai_responses", 9L, 0L, 2L)],
            rows.Select(r => (r.Api, r.Input, r.CacheRead, r.Output))
        );
        Assert.Equal(BatchResults.Key("openai_chat", "s", "r1"), rows[0].ResultKey);
    }

    [Fact]
    public void A_malformed_or_truncated_line_costs_only_itself()
    {
        var ok =
            """{"custom_id":"a","result":{"type":"succeeded","message":{"model":"m","usage":{"input_tokens":1,"output_tokens":2}}}}""";
        var body = new List<byte>(
            Encoding.UTF8.GetBytes($"not json\n{ok}\n\0\n{{\"custom_id\":\"z\",\"res")
        );
        body.AddRange([(byte)'\n', 0xff, 0xfe, (byte)'\n']);
        body.AddRange(Encoding.UTF8.GetBytes(ok));

        Assert.Equal(2, BatchResults.Rows(body.ToArray(), "s", null).Count);
        Assert.Empty(BatchResults.Rows(Array.Empty<byte>(), "s", null));
        Assert.Empty(BatchResults.Rows("{}\n[]\n42\n"u8.ToArray(), "s", null));
        var anonymous = Assert.Single(
            BatchResults.Rows(
                Jsonl(AnthropicLine.Replace("\"custom_id\":\"a\",", "", StringComparison.Ordinal)),
                "s",
                "x"
            )
        );
        Assert.Null(anonymous.ResultKey);
    }

    [Theory]
    [InlineData(null, """{"service_tier":"flex"}""", "flex")]
    [InlineData(null, """{"usage":{"service_tier":"batch"}}""", "batch")]
    [InlineData(null, """{"usage":{"input_tokens":1}}""", null)]
    [InlineData(null, "not json", null)]
    [InlineData(
        "text/event-stream",
        "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"service_tier\":\"standard\"}}}\n\ndata: {\"response\":{\"service_tier\":\"flex\"}}\n\ndata: {\"x\":1}\n\n",
        "flex"
    )]
    public void The_service_tier_is_the_response_s_word(
        string? contentType,
        string body,
        string? tier
    ) => Assert.Equal(tier, UsageReader.ServiceTier(contentType, Encoding.UTF8.GetBytes(body)));

    [Fact]
    public async Task Anthropic_results_become_usage_rows_keyed_by_where_they_came_from()
    {
        await using var h = await ProxyHarness.StartAsync(c => c.Proxy.Batch.ParseResults = true);
        const string path = "/v1/messages/batches/msgbatch_1/results";
        Serve(h.Upstream, path, Jsonl(AnthropicLine));

        using var response = await h.Client.GetAsync(h.Url(path + "?beta=true"), Ct);

        var row = Assert.Single(await h.UsageAsync(1));
        Assert.Equal((10L, 5L, "claude-sonnet-5-5"), (row.Input, row.Output, row.Model));
        Assert.Equal(BatchResults.Key("anthropic", h.Upstream.Url + path, "a"), row.ResultKey);
    }

    [Fact]
    public async Task A_results_file_fetched_twice_adds_no_row_and_is_tagged_batch()
    {
        await using var h = await ProxyHarness.StartAsync(c => c.Proxy.Batch.ParseResults = true);
        const string path = "/v1/files/file-1/content";
        Serve(
            h.Upstream,
            path,
            Jsonl(
                """{"id":"r1","custom_id":"x","response":{"status_code":200,"body":{"object":"chat.completion","model":"gpt-5","usage":{"prompt_tokens":10,"completion_tokens":2}}}}""",
                """{"id":"r2","custom_id":"x","response":{"status_code":200,"body":{"object":"chat.completion","model":"gpt-5","usage":{"prompt_tokens":10,"completion_tokens":2}}}}"""
            )
        );

        using var first = await h.Client.GetAsync(h.Url(path), Ct);
        using var second = await h.Client.GetAsync(h.Url(path), Ct);

        // The kind is set after the rows, so two re-tagged calls mean both fetches were read.
        await h.RowsAsync(
            db => db.Calls.AsNoTracking().Count(c => c.Kind == "api_request:batch"),
            n => n >= 2
        );
        var rows = await h.UsageAsync(2);
        Assert.Equal(2, rows.Count);
        Assert.Equal(20, rows.Sum(r => r.Input));
    }

    [Fact]
    public async Task Results_are_read_only_when_asked()
    {
        await using var h = await ProxyHarness.StartAsync();
        const string path = "/v1/files/file-1/content";
        Serve(h.Upstream, path, Jsonl(AnthropicLine));

        using var response = await h.Client.GetAsync(h.Url(path), Ct);

        var kind = await h.RowsAsync(
            db => db.Calls.AsNoTracking().Where(c => c.Ms != null).Select(c => c.Kind).ToList(),
            k => k.Count >= 1
        );
        Assert.Equal(["api_request:files"], kind);
        Assert.Empty(await h.UsageAsync(0));
    }

    [Fact]
    public async Task The_call_keeps_the_tier_the_response_names()
    {
        await using var h = await ProxyHarness.StartAsync();
        h.Upstream.Given(Request.Create().UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(
                        """{"service_tier":"flex","usage":{"prompt_tokens":3,"completion_tokens":1}}"""
                    )
            );

        using var response = await h.Client.PostAsync(
            h.Url("/v1/chat/completions"),
            new StringContent(
                """{"model":"gpt-5","messages":[]}""",
                Encoding.UTF8,
                "application/json"
            ),
            Ct
        );

        await h.UsageAsync(1);
        var tier = await h.RowsAsync(
            db => db.Calls.AsNoTracking().Select(c => c.ServiceTier).Single(),
            t => t is not null
        );
        Assert.Equal("flex", tier);
    }

    private static byte[] Jsonl(params string[] lines) =>
        Encoding.UTF8.GetBytes(string.Join('\n', lines.Select(l => l.Trim())));

    private static void Serve(WireMockServer upstream, string path, byte[] body) =>
        upstream
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(
                Response.Create().WithHeader("Content-Type", "application/jsonl").WithBody(body)
            );
}
