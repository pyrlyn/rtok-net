using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rtok.Proxy.Tests;

public sealed class LaneRelayTests
{
    private const string Body = """{"model":"m-1","max_tokens":1,"messages":[]}""";
    private const string Answer = """{"usage":{"input_tokens":1,"output_tokens":1}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Markers_never_reach_upstream_and_name_the_kind()
    {
        await using var h = await ProxyHarness.StartAsync();
        Answers(h.Upstream);

        using var prefixed = await PostAsync(h, "/lane/bulk/v1/messages?beta=true");
        using var marked = await PostAsync(h, "/v1/messages", lane: "internal");
        using var agent = await PostAsync(h, "/v1/messages");

        Assert.All([prefixed, marked, agent], r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var seen = h.Upstream.LogEntries.Select(e => e.RequestMessage!).ToList();
        Assert.Equal(3, seen.Count);
        Assert.All(seen, r => Assert.Equal("/v1/messages", r.Path));
        Assert.All(seen, r => Assert.False(r.Headers!.ContainsKey(Lanes.Header)));
        Assert.Equal("beta=true", seen[0].RawQuery?.TrimStart('?'));
        var kinds = await h.RowsAsync(
            db => db.Calls.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Kind).ToList(),
            k => k.Count >= 3
        );
        Assert.Equal(["api_request:bulk", "api_request:internal", "api_request"], kinds);
    }

    [Fact]
    public async Task Lanes_off_forwards_the_markers_as_sent()
    {
        await using var h = await ProxyHarness.StartAsync(c => c.Proxy.Lanes.Enabled = false);
        Answers(h.Upstream);

        using var response = await PostAsync(h, "/v1/messages", lane: "bulk");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seen = Assert.Single(h.Upstream.LogEntries).RequestMessage!;
        Assert.Equal("bulk", seen.Headers![Lanes.Header].Single());
        var kind = await h.RowsAsync(
            db => db.Calls.AsNoTracking().Select(c => c.Kind).ToList(),
            k => k.Count >= 1
        );
        Assert.Equal(["api_request"], kind);
    }

    [Fact]
    public async Task A_lane_goes_to_its_own_upstream()
    {
        using var own = WireMockServer.Start();
        Answers(own);
        await using var h = await ProxyHarness.StartAsync(c =>
            c.Proxy.Lanes.Bulk.Upstream = own.Url + "/ "
        );
        Answers(h.Upstream);

        using var bulk = await PostAsync(h, "/lane/bulk/v1/messages");
        using var agent = await PostAsync(h, "/v1/messages");

        Assert.Equal(HttpStatusCode.OK, bulk.StatusCode);
        Assert.Equal("/v1/messages", Assert.Single(own.LogEntries).RequestMessage!.Path);
        Assert.Single(h.Upstream.LogEntries);
    }

    [Fact]
    public async Task A_full_queue_answers_429_and_the_agent_lane_is_never_capped_or_routed()
    {
        await using var h = await ProxyHarness.StartAsync(c =>
        {
            c.Proxy.Lanes.Bulk.MaxInFlight = 1;
            c.Proxy.Lanes.Bulk.MaxQueued = 8;
            c.Proxy.Routing.Enabled = true;
            c.Proxy.Routing.Bulk = new()
            {
                From = ["*"],
                To = "m-small",
                Provider = "anthropic",
                Gate = "bench",
            };
        });
        h.Upstream.Given(Request.Create().WithHeader("x-slow", "1").UsingPost())
            .AtPriority(1)
            .RespondWith(Ok().WithDelay(TimeSpan.FromSeconds(4)));
        Answers(h.Upstream);

        var slow = PostAsync(h, "/lane/bulk/v1/messages", slow: true);
        // WireMock logs a request once answered, so the slot holder is given time instead.
        await Task.Delay(500, Ct);
        var queued = Enumerable
            .Range(0, 8)
            .Select(_ => PostAsync(h, "/lane/bulk/v1/messages"))
            .ToList();
        var agents = Enumerable.Range(0, 4).Select(_ => PostAsync(h, "/v1/messages", slow: true));
        await Task.Delay(500, Ct);
        using var refused = await PostAsync(h, "/lane/bulk/v1/messages");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), refused.Headers.RetryAfter?.Delta);
        Assert.Contains("rate_limit_error", await refused.Content.ReadAsStringAsync(Ct));
        var started = DateTime.UtcNow;
        var agentAnswers = await Task.WhenAll(agents);
        Assert.All(agentAnswers, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(8), "agents ran side by side");
        Assert.Equal(HttpStatusCode.OK, (await slow).StatusCode);
        foreach (var answer in await Task.WhenAll(queued))
        {
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            answer.Dispose();
        }
        foreach (var answer in agentAnswers)
        {
            answer.Dispose();
        }
        var models = h
            .Upstream.LogEntries.Select(e => e.RequestMessage!.Body!)
            .GroupBy(b => b.Contains("m-small", StringComparison.Ordinal))
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(4, models[false]);
        Assert.Equal(9, models[true]);
    }

    private static IResponseBuilder Ok() =>
        Response.Create().WithHeader("Content-Type", "application/json").WithBody(Answer);

    private static void Answers(WireMockServer upstream) =>
        upstream.Given(Request.Create().UsingAnyMethod()).AtPriority(10).RespondWith(Ok());

    private static async Task<HttpResponseMessage> PostAsync(
        ProxyHarness h,
        string path,
        string? lane = null,
        bool slow = false
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, h.Url(path))
        {
            Content = new StringContent(Body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        if (lane is not null)
        {
            request.Headers.TryAddWithoutValidation(Lanes.Header, lane);
        }
        if (slow)
        {
            request.Headers.TryAddWithoutValidation("x-slow", "1");
        }
        return await h.Client.SendAsync(request, Ct);
    }
}
