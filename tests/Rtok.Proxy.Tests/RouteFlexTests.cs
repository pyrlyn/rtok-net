using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rtok.Config;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Rtok.Proxy.Tests;

public sealed class RouteFlexTests
{
    private const string Chat = "/v1/chat/completions";
    private const string ChatBody =
        """{"model":"gpt-big","messages":[{"role":"user","content":"hi"}]}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("", "small", "openai", "bench", false)]
    [InlineData("big", "", "openai", "bench", false)]
    [InlineData("big", "small", " ", "bench", false)]
    [InlineData("big", "small", "openai", "", false)]
    [InlineData("big", "small", "openai", "bench", true)]
    public void A_rule_acts_only_when_every_field_is_set(
        string from,
        string to,
        string provider,
        string gate,
        bool active
    )
    {
        var rule = Rule(to, provider, gate, from.Length > 0 ? [from] : []);
        Assert.Equal(active, ModelRoute.Active(rule));
        Assert.Equal(active, ModelRoute.Apply(rule, Bytes("""{"model":"big"}""")) is not null);
    }

    [Fact]
    public void A_rule_swaps_only_the_model_it_matches()
    {
        var rule = Rule("small", " openai ", "bench", ["gpt-big*", "exact"]);

        var routed = ModelRoute.Apply(rule, Bytes("""{"model":"gpt-big-2","n":"é"}"""))!;

        Assert.Equal("""{"model":"small","n":"é"}""", Encoding.UTF8.GetString(routed.Body));
        Assert.Equal(("gpt-big-2", "small", "openai"), (routed.From, routed.To, routed.Provider));
        Assert.NotNull(ModelRoute.Apply(rule, Bytes("""{"model":"exact"}""")));
        Assert.Null(ModelRoute.Apply(rule, Bytes("""{"model":"exactly"}""")));
        Assert.Null(ModelRoute.Apply(rule, Bytes("""{"model":"small"}""")));
        Assert.Null(ModelRoute.Apply(rule, Bytes("""{"model":1}""")));
        Assert.Null(ModelRoute.Apply(rule, Bytes("not json")));
    }

    [Fact]
    public async Task Bulk_is_routed_and_recorded_under_the_served_model_and_agent_never_is()
    {
        await using var h = await ProxyHarness.StartAsync(c =>
        {
            c.Proxy.Routing.Enabled = true;
            c.Proxy.Routing.Bulk = Rule("gpt-small", "cheap", "bench", ["*"]);
            c.Proxy.Routing.Internal = Rule("gpt-small", "cheap", "bench", ["*"]);
        });
        Answer(h, 200);

        using var bulk = await PostAsync(h, "/lane/bulk" + Chat, ChatBody);
        using var agent = await PostAsync(h, Chat, ChatBody);

        var sent = h
            .Upstream.LogEntries.Select(e => e.RequestMessage!.Body!)
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Contains("\"model\":\"gpt-big\"", sent[0], StringComparison.Ordinal);
        Assert.Contains("\"model\":\"gpt-small\"", sent[1], StringComparison.Ordinal);
        var calls = await h.RowsAsync(
            db =>
                db.Calls.AsNoTracking()
                    .OrderBy(c => c.Id)
                    .Select(c => new
                    {
                        c.Kind,
                        Provider = db.Providers.Single(p => p.Id == c.ProviderId).Slug,
                        Model = db.Models.Single(m => m.Id == c.ModelId).Slug,
                    })
                    .ToList()
                    .Select(c => (c.Kind, c.Provider, c.Model))
                    .ToList(),
            r => r.Count >= 2
        );
        Assert.Equal(
            [("api_request:bulk", "cheap", "gpt-small"), ("api_request", "openai", "gpt-big")],
            calls
        );
        var route = await h.RowsAsync(
            db => db.Measurements.AsNoTracking().Where(m => m.Kind == "model_route").ToList(),
            r => r.Count >= 1
        );
        Assert.Single(route);
    }

    [Fact]
    public void Flex_is_spliced_in_and_left_alone_where_the_client_chose()
    {
        var policy = new FlexPolicy();
        Assert.Equal(
            """ {"service_tier":"flex","model":"m"}""",
            Text(Flex.Apply("openai", Bytes(""" {"model":"m"}"""), policy))
        );
        Assert.Equal(
            """{"service_tier":"flex"}""",
            Text(Flex.Apply("openai", Bytes("{}"), policy))
        );
        Assert.Equal(
            """{"model":"m","service_tier":"flex"}""",
            Text(Flex.Apply("openai", Bytes("""{"model":"m","service_tier":null}"""), policy))
        );
        Assert.Null(Flex.Apply("openai", Bytes("""{"service_tier":"priority"}"""), policy));
        Assert.Null(Flex.Apply("anthropic", Bytes("{}"), policy));
        Assert.Null(Flex.Apply("openai", Bytes("[1]"), policy));
        policy.Force = true;
        Assert.Equal(
            """{"service_tier":"flex"}""",
            Text(Flex.Apply("openai", Bytes("""{"service_tier":"priority"}"""), policy))
        );
        Assert.Null(Flex.Apply("openai", Bytes("""{"service_tier":"flex"}"""), policy));
    }

    [Fact]
    public async Task Flex_requests_carry_the_tier_and_none_hands_the_429_over()
    {
        await using var h = await FlexAsync("none");
        Answer(h, 429);

        using var response = await PostAsync(h, "/lane/bulk" + Chat, ChatBody);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var seen = Assert.Single(h.Upstream.LogEntries).RequestMessage!.Body!;
        Assert.StartsWith(
            """{"service_tier":"flex","model":"gpt-big",""",
            seen,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(9u, 1000ul, null, new[] { 1, 2, 4, 8, 16 })]
    [InlineData(2u, 1000ul, null, new[] { 1, 2 })]
    [InlineData(5u, 10_000ul, null, new[] { 10, 20, 30, 30, 30 })]
    [InlineData(3u, 1000ul, "5", new[] { 5, 5, 5 })]
    [InlineData(3u, 1000ul, "31", new int[0])]
    [InlineData(3u, 1000ul, "Wed, 21 Oct 2015 07:28:00 GMT", new[] { 1, 2, 4 })]
    public async Task Backoff_doubles_under_a_30s_cap_and_at_most_five_retries(
        uint retries,
        ulong backoffMs,
        string? retryAfter,
        int[] waits
    )
    {
        var time = new InstantTime();
        await using var h = await FlexAsync(
            "backoff",
            c =>
            {
                c.Proxy.Flex.Retries = retries;
                c.Proxy.Flex.BackoffMs = backoffMs;
            },
            time
        );
        Answer(h, 429, retryAfter);

        using var response = await PostAsync(h, "/lane/bulk" + Chat, ChatBody);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(waits.Length + 1, h.Upstream.LogEntries.Count);
        Assert.Equal(waits.Select(s => TimeSpan.FromSeconds(s)), time.Waits);
    }

    [Theory]
    [InlineData(ChatBody, "\"service_tier\":\"auto\"")]
    [InlineData(
        """{"model":"gpt-big","service_tier":"priority","messages":[]}""",
        "\"service_tier\":\"priority\""
    )]
    public async Task Default_retries_once_on_the_clients_tier_or_auto(string body, string tier)
    {
        await using var h = await FlexAsync("default", c => c.Proxy.Flex.Force = true);
        h.Upstream.Given(
                Request
                    .Create()
                    .WithBody(new WildcardMatcher("*\"service_tier\":\"flex\"*"))
                    .UsingPost()
            )
            .AtPriority(1)
            .RespondWith(Response.Create().WithStatusCode(429));
        Answer(h, 200);

        using var response = await PostAsync(h, "/lane/bulk" + Chat, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = h.Upstream.LogEntries.Select(e => e.RequestMessage!.Body!).ToList();
        Assert.Equal(2, sent.Count);
        Assert.Contains(tier, sent[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_408_and_a_tier_the_client_chose_are_never_retried()
    {
        await using var h = await FlexAsync("default");
        h.Upstream.Given(Request.Create().WithBody(new WildcardMatcher("*gpt-big*")).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithStatusCode(408));
        Answer(h, 429);
        using var timedOut = await PostAsync(h, "/lane/bulk" + Chat, ChatBody);
        using var chosen = await PostAsync(
            h,
            "/lane/bulk" + Chat,
            """{"model":"m","service_tier":"flex"}"""
        );

        Assert.Equal(HttpStatusCode.RequestTimeout, timedOut.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, chosen.StatusCode);
        Assert.Equal(2, h.Upstream.LogEntries.Count);
    }

    [Fact]
    public async Task The_agent_lane_never_gets_flex()
    {
        await using var h = await FlexAsync("backoff");
        Answer(h, 200);

        using var response = await PostAsync(h, Chat, ChatBody);

        var seen = Assert.Single(h.Upstream.LogEntries).RequestMessage!.Body!;
        Assert.DoesNotContain("service_tier", seen, StringComparison.Ordinal);
    }

    private static Task<ProxyHarness> FlexAsync(
        string on429,
        Action<RtokConfig>? more = null,
        TimeProvider? time = null
    ) =>
        ProxyHarness.StartAsync(
            c =>
            {
                c.Proxy.Lanes.Bulk.Flex = true;
                c.Proxy.Flex.On429 = on429;
                more?.Invoke(c);
            },
            o => o with { Time = time ?? new InstantTime() }
        );

    private static RouteRule Rule(string to, string provider, string gate, IList<string> from) =>
        new()
        {
            From = from,
            To = to,
            Provider = provider,
            Gate = gate,
        };

    private static void Answer(ProxyHarness h, int status, string? retryAfter = null)
    {
        var response = Response
            .Create()
            .WithStatusCode(status)
            .WithHeader("Content-Type", "application/json")
            .WithBody("""{"usage":{"prompt_tokens":1,"completion_tokens":1}}""");
        if (retryAfter is not null)
        {
            response = response.WithHeader("Retry-After", retryAfter);
        }
        h.Upstream.Given(Request.Create().UsingPost()).AtPriority(10).RespondWith(response);
    }

    private static async Task<HttpResponseMessage> PostAsync(
        ProxyHarness h,
        string path,
        string body
    )
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await h.Client.PostAsync(h.Url(path), content, Ct);
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string? Text(byte[]? bytes) =>
        bytes is null ? null : Encoding.UTF8.GetString(bytes);
}
