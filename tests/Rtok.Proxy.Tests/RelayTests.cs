using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Rtok.Proxy.Tests;

public sealed class RelayTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Messages = "/v1/messages";

    [Fact]
    public async Task Plain_mode_passes_bytes_and_headers_through_and_records_nothing()
    {
        await using var h = await ProxyHarness.StartAsync(c => c.Proxy.Enabled = false);
        var payload = new byte[300_000];
        new Random(7).NextBytes(payload);
        h.Upstream.Given(Request.Create().UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(201)
                    .WithHeader("Content-Type", "application/octet-stream")
                    .WithHeader("Content-Encoding", "gzip")
                    .WithHeader("x-upstream", "yes")
                    .WithBody(payload)
            );
        using var request = new HttpRequestMessage(HttpMethod.Post, h.Url("/raw/x%2Fy?q=1"))
        {
            Content = new ByteArrayContent([1, 2, 3]),
        };
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");

        using var response = await h.Client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(payload, body);
        Assert.Equal("yes", response.Headers.GetValues("x-upstream").Single());
        Assert.Equal("gzip", response.Content.Headers.ContentEncoding.Single());
        var seen = Assert.Single(h.Upstream.LogEntries).RequestMessage!;
        Assert.Equal([1, 2, 3], seen.BodyAsBytes);
        Assert.Equal("gzip", seen.Headers!["Accept-Encoding"].Single());
        Assert.Equal("1", seen.Headers[Hops.Header].Single());
        Assert.Empty(await h.UsageAsync(0));
        Assert.Equal(1, h.Server.Live.Count);
    }

    [Fact]
    public async Task Anthropic_json_records_one_usage_row_and_the_call()
    {
        await using var h = await ProxyHarness.StartAsync();
        Json(
            h,
            Messages,
            """{"id":"m","usage":{"input_tokens":11,"cache_creation_input_tokens":2,"cache_read_input_tokens":0,"output_tokens":4}}"""
        );

        var response = await PostAsync(
            h,
            Messages,
            """{"model":"claude-x","metadata":{"user_id":"s-1"},"messages":[]}""",
            ("Accept-Encoding", "gzip")
        );

        Assert.Equal(HttpStatusCode.OK, response);
        var usage = Assert.Single(await h.UsageAsync(1));
        Assert.Equal(("anthropic", "s-1", "claude-x"), (usage.Api, usage.Session, usage.Model));
        Assert.Equal(
            (11L, 2L, 0L, 4L),
            (usage.Input, usage.CacheCreate, usage.CacheRead, usage.Output)
        );
        var call = await h.RowsAsync(
            db => db.Calls.AsNoTracking().Single(c => c.Id == usage.CallId),
            c => c.Ms is not null
        );
        Assert.Equal(("proxy", "api_request", Messages), (call.Surface, call.Kind, call.Name));
        Assert.NotNull(call.Ms);
        var io = await h.RowsAsync(
            db => db.CallIo.AsNoTracking().SingleOrDefault(c => c.CallId == usage.CallId),
            c => c is not null
        );
        Assert.True(io!.ResponseBytes > 0);
        var tokens = await h.RowsAsync(
            db => db.Tokens.AsNoTracking().Where(t => t.CallId == usage.CallId).ToList(),
            t => t.Count > 0
        );
        Assert.Equal(17, Assert.Single(tokens).Tokens);
        Assert.Equal(
            "identity",
            Assert
                .Single(h.Upstream.LogEntries)
                .RequestMessage!.Headers!["Accept-Encoding"]
                .Single()
        );
    }

    [Fact]
    public async Task A_prompt_cache_hit_is_still_one_row()
    {
        await using var h = await ProxyHarness.StartAsync();
        Json(
            h,
            Messages,
            """{"usage":{"input_tokens":3,"cache_read_input_tokens":4000,"output_tokens":9}}"""
        );

        await PostAsync(h, Messages, """{"model":"claude-x","metadata":{"user_id":"s"}}""");

        var usage = Assert.Single(await h.UsageAsync(1));
        Assert.Equal(
            (3L, 0L, 4000L, 9L),
            (usage.Input, usage.CacheCreate, usage.CacheRead, usage.Output)
        );
    }

    [Fact]
    public async Task Openai_chat_stream_asks_for_usage_and_records_it()
    {
        await using var h = await ProxyHarness.StartAsync();
        Sse(
            h,
            "/v1/chat/completions",
            "data: {\"choices\":[{\"delta\":{\"content\":\"a\"}}]}\n\n"
                + "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":20,\"completion_tokens\":5,\"prompt_tokens_details\":{\"cached_tokens\":8}}}\n\n"
                + "data: [DONE]\n\n"
        );

        await PostAsync(
            h,
            "/v1/chat/completions",
            """{"model":"gpt-x","user":"u","stream":true}"""
        );

        var usage = Assert.Single(await h.UsageAsync(1));
        Assert.Equal(("openai_chat", "u", "gpt-x"), (usage.Api, usage.Session, usage.Model));
        Assert.Equal((12L, 8L, 5L), (usage.Input, usage.CacheRead, usage.Output));
        using var sent = JsonDocument.Parse(
            Assert.Single(h.Upstream.LogEntries).RequestMessage!.Body!
        );
        Assert.True(
            sent.RootElement.GetProperty("stream_options").GetProperty("include_usage").GetBoolean()
        );
    }

    [Theory]
    [InlineData("/v1/messages", "application/json", "anthropic", 5, 2, 3, 4)]
    [InlineData("/v1/messages", "text/event-stream", "anthropic", 5, 2, 3, 4)]
    [InlineData("/v1/chat/completions", "application/json", "openai_chat", 5, 0, 3, 4)]
    [InlineData("/v1/chat/completions", "text/event-stream", "openai_chat", 5, 0, 3, 4)]
    [InlineData("/v1/responses", "application/json", "openai_responses", 5, 0, 3, 4)]
    [InlineData("/v1/responses", "text/event-stream", "openai_responses", 5, 0, 3, 4)]
    [InlineData(GeminiPath + ":generateContent", "application/json", "gemini", 5, 0, 3, 6)]
    [InlineData(GeminiPath + ":streamGenerateContent", "text/event-stream", "gemini", 5, 0, 3, 6)]
    public async Task Each_wire_records_one_row_with_disjoint_counters(
        string path,
        string contentType,
        string api,
        long input,
        long cacheCreate,
        long cacheRead,
        long output
    )
    {
        await using var h = await ProxyHarness.StartAsync();
        h.Upstream.Given(Request.Create().UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", contentType)
                    .WithBody(Answer(api, contentType))
            );

        await PostAsync(
            h,
            path,
            """{"model":"m-1","user":"u","metadata":{"user_id":"u"}}""",
            ("x-rtok-session", "u")
        );

        var usage = Assert.Single(await h.UsageAsync(1));
        Assert.Equal((api, "u"), (usage.Api, usage.Session));
        Assert.Equal(
            string.Equals(api, "gemini", StringComparison.Ordinal) ? "gemini-2.5-pro" : "m-1",
            usage.Model
        );
        Assert.Equal(
            (input, cacheCreate, cacheRead, output),
            (usage.Input, usage.CacheCreate, usage.CacheRead, usage.Output)
        );
    }

    [Fact]
    public async Task A_stream_longer_than_the_tee_reaches_the_client_whole()
    {
        await using var h = await ProxyHarness.StartAsync(options: o => o with { TeeBytes = 4096 });
        var start =
            "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":42,\"output_tokens\":1}}}\n\n";
        var filler = new StringBuilder(start);
        while (filler.Length < 400_000)
        {
            filler
                .Append("event: content_block_delta\ndata: {\"delta\":{\"text\":\"")
                .Append('x', 500)
                .Append("\"}}\n\n");
        }
        var sse = filler.ToString();
        Sse(h, Messages, sse);

        using var response = await h.Client.PostAsync(
            h.Url(Messages),
            new StringContent("""{"model":"claude-x","metadata":{"user_id":"s"},"stream":true}"""),
            Ct
        );
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(sse, body);
        var usage = Assert.Single(await h.UsageAsync(1));
        Assert.Equal((42L, 1L), (usage.Input, usage.Output));
        var io = await h.RowsAsync(
            db => db.CallIo.AsNoTracking().SingleOrDefault(c => c.CallId == usage.CallId),
            c => c is not null
        );
        Assert.Equal(4096, io!.ResponseBytes);
    }

    [Fact]
    public async Task An_upstream_error_status_is_relayed_with_a_zero_row()
    {
        await using var h = await ProxyHarness.StartAsync();
        h.Upstream.Given(Request.Create().WithPath(Messages).UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(529)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"type":"error","error":{"type":"overloaded_error"}}""")
            );

        using var response = await h.Client.PostAsync(
            h.Url(Messages),
            new StringContent("""{"model":"claude-x","metadata":{"user_id":"s"}}"""),
            Ct
        );

        Assert.Equal(529, (int)response.StatusCode);
        Assert.Contains(
            "overloaded_error",
            await response.Content.ReadAsStringAsync(Ct),
            StringComparison.Ordinal
        );
        var usage = Assert.Single(await h.UsageAsync(1));
        Assert.Equal(0, usage.Input + usage.CacheCreate + usage.CacheRead + usage.Output);
    }

    [Fact]
    public async Task An_unreachable_upstream_is_a_502_with_a_zero_row()
    {
        var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var port = ((IPEndPoint)closed.LocalEndpoint).Port;
        closed.Stop();
        await using var h = await ProxyHarness.StartAsync(c =>
            c.Proxy.Upstream = $"http://127.0.0.1:{port}"
        );

        using var response = await h.Client.PostAsync(
            h.Url(Messages),
            new StringContent("""{"model":"claude-x","metadata":{"user_id":"s"}}"""),
            Ct
        );

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(
            "upstream_error",
            error.RootElement.GetProperty("error").GetProperty("type").GetString()
        );
        Assert.StartsWith(
            "rtok proxy: upstream error: POST /v1/messages",
            error.RootElement.GetProperty("error").GetProperty("message").GetString(),
            StringComparison.Ordinal
        );
        var usage = Assert.Single(await h.UsageAsync(1));
        Assert.Equal(0, usage.Input + usage.Output);
        var call = await h.RowsAsync(
            db => db.Calls.AsNoTracking().Single(c => c.Id == usage.CallId),
            c => c.Ms is not null
        );
        Assert.NotNull(call.Ms);
    }

    [Fact]
    public async Task A_silent_upstream_times_out_per_read()
    {
        await using var h = await ProxyHarness.StartAsync(c => c.Proxy.TimeoutS = 1);
        h.Upstream.Given(Request.Create().WithPath(Messages).UsingPost())
            .RespondWith(Response.Create().WithDelay(TimeSpan.FromSeconds(4)).WithBody("{}"));

        using var response = await h.Client.PostAsync(h.Url(Messages), new StringContent("{}"), Ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains(
            "no response within 1s",
            await response.Content.ReadAsStringAsync(Ct),
            StringComparison.Ordinal
        );
        Assert.Single(await h.UsageAsync(1));
    }

    [Fact]
    public async Task A_request_another_rtok_saw_is_forwarded_plain_and_a_loop_is_refused()
    {
        var log = new LogCapture();
        await using var h = await ProxyHarness.StartAsync(options: o =>
            o with
            {
                Logger = log.Logger,
            }
        );
        Json(h, Messages, """{"usage":{"input_tokens":1,"output_tokens":1}}""");

        var once = await PostAsync(h, Messages, "{}", (Hops.Header, "1"));
        var junk = await PostAsync(h, Messages, "{}", (Hops.Header, "yes"));
        var loop = await PostAsync(h, Messages, "{}", (Hops.Header, "8"));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (once, junk));
        Assert.Equal((HttpStatusCode)508, loop);
        Assert.All(
            h.Upstream.LogEntries,
            e => Assert.Equal("2", e.RequestMessage!.Headers![Hops.Header].Single())
        );
        Assert.Equal(2, h.Upstream.LogEntries.Count);
        Assert.Empty(await h.UsageAsync(0));
        Assert.Equal(
            2,
            log.Events.Count(e =>
                e.Level == Serilog.Events.LogEventLevel.Warning
                && e.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("rtok proxy hop", StringComparison.Ordinal)
            )
        );
        Assert.Single(log.Events, e => e.Level == Serilog.Events.LogEventLevel.Error);
    }

    [Fact]
    public async Task Health_and_live_answer_locally()
    {
        await using var recording = await ProxyHarness.StartAsync();
        await using var plain = await ProxyHarness.StartAsync(c => c.Core.Enabled = false);
        plain
            .Upstream.Given(Request.Create().UsingGet())
            .RespondWith(Response.Create().WithBody("ok"));
        await plain.Client.GetAsync(plain.Url("/v1/models"), Ct);

        var on = await recording.Client.GetStringAsync(recording.Url("/health"), Ct);
        var off = await plain.Client.GetStringAsync(plain.Url("/health"), Ct);
        var live = await plain.Client.GetStringAsync(plain.Url("/live"), Ct);
        using var post = await plain.Client.PostAsync(
            plain.Url("/health"),
            new StringContent(""),
            Ct
        );

        Assert.Equal("""{"ok":true,"mode":"passthrough","enabled":true,"recording":true}""", on);
        Assert.Equal(
            """{"ok":true,"mode":"passthrough","enabled":false,"recording":false,"live":1}""",
            off
        );
        using var entries = JsonDocument.Parse(live);
        var entry = Assert.Single(entries.RootElement.EnumerateArray());
        Assert.Equal("/v1/models", entry.GetProperty("path").GetString());
        Assert.Equal(2, entry.GetProperty("response_bytes").GetInt64());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Empty(recording.Upstream.LogEntries);
    }

    [Fact]
    public async Task The_path_and_query_go_upstream_as_the_client_sent_them()
    {
        var seen = new List<Uri>();
        await using var h = await ProxyHarness.StartAsync(
            c => c.Proxy.Upstream = "https://gw.example/base/",
            o => o with { Upstream = new Capture(seen) }
        );

        await PostAsync(h, "/v1/skills/a%2Fb%20c?x=%2F&y", "");

        Assert.Equal(
            "https://gw.example/base/v1/skills/a%2Fb%20c?x=%2F&y",
            Assert.Single(seen).OriginalString
        );
    }

    private const string GeminiPath = "/v1beta/models/gemini-2.5-pro";

    /// <summary>One wire's answer with input 5 (uncached), cache write 2 where the wire has one,
    /// cache read 3 and output 4 (Gemini: plus 2 thoughts).</summary>
    private static string Answer(string api, string contentType)
    {
        var sse = UsageReader.IsSse(contentType);
        return (api, sse) switch
        {
            ("anthropic", false) =>
                """{"usage":{"input_tokens":5,"cache_creation_input_tokens":2,"cache_read_input_tokens":3,"output_tokens":4}}""",
            ("anthropic", true) =>
                "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":5,\"cache_creation_input_tokens\":2,\"cache_read_input_tokens\":3,\"output_tokens\":1}}}\n\n"
                    + "data: {\"type\":\"message_delta\",\"usage\":{\"output_tokens\":4}}\n\n",
            ("openai_chat", false) =>
                """{"usage":{"prompt_tokens":8,"completion_tokens":4,"prompt_tokens_details":{"cached_tokens":3}}}""",
            ("openai_chat", true) => "data: {\"choices\":[{\"delta\":{}}]}\n\n"
                + "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":8,\"completion_tokens\":4,\"prompt_tokens_details\":{\"cached_tokens\":3}}}\n\ndata: [DONE]\n\n",
            ("openai_responses", false) =>
                """{"usage":{"input_tokens":8,"output_tokens":4,"input_tokens_details":{"cached_tokens":3}}}""",
            ("openai_responses", true) =>
                "data: {\"type\":\"response.created\",\"response\":{\"usage\":{\"input_tokens\":99}}}\n\n"
                    + "data: {\"type\":\"response.completed\",\"response\":{\"usage\":{\"input_tokens\":8,\"output_tokens\":4,\"input_tokens_details\":{\"cached_tokens\":3}}}}\n\n",
            ("gemini", false) =>
                """{"usageMetadata":{"promptTokenCount":8,"cachedContentTokenCount":3,"candidatesTokenCount":4,"thoughtsTokenCount":2}}""",
            _ => "data: {\"candidates\":[]}\n\n"
                + "data: {\"usageMetadata\":{\"promptTokenCount\":8,\"cachedContentTokenCount\":3,\"candidatesTokenCount\":4,\"thoughtsTokenCount\":2}}\n\n",
        };
    }

    private static void Json(ProxyHarness h, string path, string body) =>
        h
            .Upstream.Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(body)
            );

    private static void Sse(ProxyHarness h, string path, string body) =>
        h
            .Upstream.Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "text/event-stream")
                    .WithBody(body)
            );

    private static async Task<HttpStatusCode> PostAsync(
        ProxyHarness h,
        string path,
        string body,
        params (string Name, string Value)[] headers
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, h.Url(path))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
        using var response = await h.Client.SendAsync(request, Ct);
        await response.Content.ReadAsByteArrayAsync(Ct);
        return response.StatusCode;
    }

    /// <summary>An upstream that answers 200 and keeps each request's URI.</summary>
    private sealed class Capture(List<Uri> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            seen.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
