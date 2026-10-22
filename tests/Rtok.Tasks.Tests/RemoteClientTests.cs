using System.Net;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.Tests;

public sealed class RemoteClientTests : IDisposable
{
    private readonly FakeService _fake = new();

    public void Dispose() => _fake.Dispose();

    private RemoteClient Client() =>
        new(_fake.Http, "github", _fake.Url, _ => { }, _fake.Clock, TimeSpan.Zero);

    [Theory]
    [InlineData(403, "retry-after", "7", 7)]
    [InlineData(429, "retry-after", "0", 0)]
    [InlineData(403, "x-ratelimit-remaining", "0", 30)]
    [InlineData(429, "x-other", "1", 60)]
    [InlineData(403, "x-other", "1", -1)]
    [InlineData(500, "retry-after", "7", -1)]
    public void Rate_limits_wait_by_their_headers(
        int status,
        string header,
        string value,
        int seconds
    )
    {
        using var response = new HttpResponseMessage((HttpStatusCode)status);
        response.Headers.TryAddWithoutValidation(header, value);
        if (string.Equals(header, "x-ratelimit-remaining", StringComparison.Ordinal))
        {
            response.Headers.TryAddWithoutValidation("x-ratelimit-reset", "1030");
        }

        var wait = RemoteClient.RateWait(response.StatusCode, response.Headers, 1000);

        Assert.Equal(seconds < 0 ? null : TimeSpan.FromSeconds(seconds), wait);
    }

    [Theory]
    [InlineData("""<https://x/2>; rel="next", <https://x/9>; rel="last" """, "https://x/2")]
    [InlineData("""<https://x/1>; rel="prev", <https://x/3>; rel = "next" """, "https://x/3")]
    [InlineData("""<https://x/9>; rel="last" """, null)]
    [InlineData("", null)]
    public void Link_headers_name_the_next_page(string link, string? next) =>
        Assert.Equal(next, RemoteClient.NextLink(link));

    [Fact]
    public async Task A_rate_limit_is_tried_three_times_then_reported()
    {
        _fake.On(
            "GET",
            "/x",
            "{}",
            status: 429,
            headers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retry-after"] = "0",
            }
        );

        var e = await Assert.ThrowsAsync<TaskException>(() =>
            Client().SendAsync(HttpMethod.Get, $"{_fake.Url}/x", null, Ct)
        );

        Assert.Equal($"github: GET {_fake.Url}/x: rate limited; try again in 0 s", e.Message);
        Assert.Equal(3, _fake.Calls.Count);
    }

    [Fact]
    public async Task Writes_are_paced_a_second_apart_and_reads_are_not()
    {
        _fake.On("POST", "/x", "{}");
        _fake.On("GET", "/x", "{}");
        var clock = new StepClock(1_800_000_000);
        var client = new RemoteClient(
            _fake.Http,
            "github",
            _fake.Url,
            _ => { },
            clock,
            TimeSpan.FromSeconds(1)
        );

        await client.SendAsync(HttpMethod.Post, $"{_fake.Url}/x", "{}", Ct);
        await client.SendAsync(HttpMethod.Get, $"{_fake.Url}/x", null, Ct);
        await client.SendAsync(HttpMethod.Post, $"{_fake.Url}/x", "{}", Ct);
        await client.SendAsync(HttpMethod.Post, $"{_fake.Url}/x", "{}", Ct);

        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)], clock.Delays);
        Assert.Equal(4, _fake.Calls.Count);
    }

    [Fact]
    public async Task A_retry_after_is_waited_out_on_the_clock()
    {
        _fake.On(
            "GET",
            "/x",
            "{}",
            status: 403,
            headers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retry-after"] = "7",
            }
        );
        var clock = new StepClock(1_800_000_000);
        var client = new RemoteClient(
            _fake.Http,
            "github",
            _fake.Url,
            _ => { },
            clock,
            TimeSpan.Zero
        );

        await Assert.ThrowsAsync<TaskException>(() =>
            client.SendAsync(HttpMethod.Get, $"{_fake.Url}/x", null, Ct)
        );

        Assert.Equal([TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(7)], clock.Delays);
        Assert.Equal(3, _fake.Calls.Count);
    }

    [Fact]
    public async Task A_long_rate_limit_is_reported_at_once()
    {
        _fake.On(
            "GET",
            "/x",
            "{}",
            status: 403,
            headers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retry-after"] = "61",
            }
        );

        var e = await Assert.ThrowsAsync<TaskException>(() =>
            Client().SendAsync(HttpMethod.Get, $"{_fake.Url}/x", null, Ct)
        );

        Assert.EndsWith("rate limited; try again in 61 s", e.Message, StringComparison.Ordinal);
        Assert.Single(_fake.Calls);
    }

    [Theory]
    [InlineData(
        """{"message":"Resource not accessible by integration"}""",
        "Resource not accessible by integration"
    )]
    [InlineData("""{"errors":[{"message":"Validation Failed"}]}""", "Validation Failed")]
    [InlineData("plain words", "plain words")]
    public async Task A_refusal_carries_the_service_message_and_never_the_token(
        string body,
        string message
    )
    {
        _fake.On("POST", "/x", body, status: 403);
        var client = new RemoteClient(
            _fake.Http,
            "github",
            _fake.Url,
            r => r.Headers.Authorization = new("Bearer", "secret-token"),
            _fake.Clock,
            TimeSpan.Zero
        );

        var e = await Assert.ThrowsAsync<TaskException>(() =>
            client.SendAsync(HttpMethod.Post, $"{_fake.Url}/x", "{}", Ct)
        );

        Assert.Equal($"github: POST {_fake.Url}/x: 403 Forbidden: {message}", e.Message);
        Assert.DoesNotContain("secret", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_is_named()
    {
        _fake.On("GET", "/x", "<html>");

        var e = await Assert.ThrowsAsync<TaskException>(() =>
            Client().SendAsync(HttpMethod.Get, $"{_fake.Url}/x", null, Ct)
        );

        Assert.Equal($"github: GET {_fake.Url}/x: unexpected JSON", e.Message);
    }

    [Fact]
    public async Task Pages_are_followed_within_the_base_only()
    {
        var url = _fake.Url;
        _fake.On(
            "GET",
            "/p1",
            "[1,2]",
            headers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Link"] = $"<{url}/p2>; rel=\"next\"",
            }
        );
        _fake.On(
            "GET",
            "/p2",
            "[3]",
            headers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Link"] = "<https://elsewhere.example/p3>; rel=\"next\"",
            }
        );

        var items = await Client().GetAllAsync($"{url}/p1", Ct);

        Assert.Equal([1, 2, 3], items.Select(i => i.GetInt32()));
        Assert.Equal(2, _fake.Calls.Count);
    }
}
