using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rtok.Proxy.Tests;

public sealed class CacheRelayTests
{
    private const string Ask =
        """{"model":"m","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""";
    private const string Answer =
        """{"content":[{"type":"text","text":"yo"}],"usage":{"input_tokens":3,"output_tokens":1}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_second_identical_question_is_answered_from_the_cache()
    {
        await using var h = await StartAsync();
        Answers(h.Upstream, 200);

        using var first = await PostAsync(h, "/v1/messages", Ask);
        using var second = await PostAsync(h, "/v1/messages", Ask);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(Answer, await second.Content.ReadAsStringAsync(Ct));
        Assert.Equal("application/json", second.Content.Headers.ContentType?.MediaType);
        Assert.Single(h.Upstream.LogEntries);
        var hit = await h.RowsAsync(
            db =>
                db.Measurements.AsNoTracking().Where(m => m.Kind == "semantic_cache_hit").ToList(),
            m => m.Count >= 1
        );
        var row = Assert.Single(hit);
        Assert.Equal(Answer.Length, row.BeforeBytes);
        Assert.StartsWith("1.0000:", row.RefId, StringComparison.Ordinal);
        Assert.Equal(2, (await h.UsageAsync(2)).Count);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task An_error_answer_is_never_cached(int status)
    {
        await using var h = await StartAsync();
        Answers(h.Upstream, status);

        using var first = await PostAsync(h, "/v1/messages", Ask);
        using var second = await PostAsync(h, "/v1/messages", Ask);

        Assert.Equal(status, (int)second.StatusCode);
        Assert.Equal(2, h.Upstream.LogEntries.Count);
    }

    [Fact]
    public async Task A_body_the_tee_could_not_keep_whole_is_never_cached()
    {
        await using var h = await StartAsync(options: o => o with { TeeBytes = 16 });
        Answers(h.Upstream, 200);

        using var first = await PostAsync(h, "/v1/messages", Ask);
        using var second = await PostAsync(h, "/v1/messages", Ask);

        Assert.Equal(Answer, await second.Content.ReadAsStringAsync(Ct));
        Assert.Equal(2, h.Upstream.LogEntries.Count);
    }

    [Theory]
    [InlineData("/v1/responses", """{"model":"m","input":"hi"}""")]
    [InlineData(
        "/v1/messages",
        """{"model":"m","stream":true,"messages":[{"role":"user","content":"hi"}]}"""
    )]
    [InlineData(
        "/v1/messages",
        """{"model":"m","messages":[{"role":"user","content":"a"},{"role":"user","content":"b"}]}"""
    )]
    public async Task Ineligible_requests_always_go_upstream(string path, string body)
    {
        await using var h = await StartAsync();
        Answers(h.Upstream, 200);

        using var first = await PostAsync(h, path, body);
        using var second = await PostAsync(h, path, body);

        Assert.Equal(2, h.Upstream.LogEntries.Count);
    }

    [Fact]
    public async Task Two_callers_never_share_an_answer()
    {
        await using var h = await StartAsync();
        Answers(h.Upstream, 200);

        using var a = await PostAsync(h, "/v1/messages", Ask, key: "a");
        using var b = await PostAsync(h, "/v1/messages", Ask, key: "b");
        using var again = await PostAsync(h, "/v1/messages", Ask, key: "a");

        Assert.Equal(2, h.Upstream.LogEntries.Count);
    }

    [Fact]
    public async Task Off_by_default()
    {
        await using var h = await ProxyHarness.StartAsync();
        Answers(h.Upstream, 200);

        using var first = await PostAsync(h, "/v1/messages", Ask);
        using var second = await PostAsync(h, "/v1/messages", Ask);

        Assert.Equal(2, h.Upstream.LogEntries.Count);
    }

    [Fact]
    public void An_entry_lives_for_the_ttl_only()
    {
        var time = new SteppedTime();
        var cache = new ResponseCache(time);
        var ttl = TimeSpan.FromSeconds(10);
        byte[] key = [1, 2];
        cache.Store(key, ttl, new CachedAnswer(200, null, [7]));
        cache.Store([3], ttl, new CachedAnswer(503, null, [7]));

        Assert.NotNull(cache.Lookup(key, ttl));
        Assert.Null(cache.Lookup([3], ttl));
        time.Now += TimeSpan.FromSeconds(10);
        Assert.Null(cache.Lookup(key, ttl));
    }

    private static Task<ProxyHarness> StartAsync(
        Func<ProxyOptions, ProxyOptions>? options = null
    ) => ProxyHarness.StartAsync(c => c.Plugins.Proxy.SemanticCache.Enabled = true, options);

    private static void Answers(WireMockServer upstream, int status) =>
        upstream
            .Given(Request.Create().UsingAnyMethod())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(status)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(Answer)
            );

    private static async Task<HttpResponseMessage> PostAsync(
        ProxyHarness h,
        string path,
        string body,
        string key = "k"
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, h.Url(path))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("x-api-key", key);
        var response = await h.Client.SendAsync(request, Ct);
        await response.Content.LoadIntoBufferAsync(Ct);
        return response;
    }

    /// <summary>A clock that moves only when told.</summary>
    private sealed class SteppedTime : TimeProvider
    {
        public TimeSpan Now { get; set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Now.Ticks;
    }
}
