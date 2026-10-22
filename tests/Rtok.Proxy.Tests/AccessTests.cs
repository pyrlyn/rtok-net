using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Rtok.Config;

namespace Rtok.Proxy.Tests;

/// <summary>rtok S5 and S6 on the handler itself, so a peer other than loopback needs no second
/// network interface.</summary>
public sealed class AccessTests
{
    private static readonly IPAddress Remote = IPAddress.Parse("192.0.2.10");

    [Fact]
    public async Task Another_host_needs_the_token_for_health_and_live()
    {
        var handler = Handler(token: "secret");

        Assert.Equal(403, (await SendAsync(handler, "/health", Remote)).Status);
        Assert.Equal(403, (await SendAsync(handler, "/live", Remote, query: "?token=nope")).Status);
        Assert.Equal(
            200,
            (await SendAsync(handler, "/live", Remote, query: "?token=secret")).Status
        );
        Assert.Equal(
            200,
            (await SendAsync(handler, "/health", Remote, authorization: "Bearer secret")).Status
        );
        Assert.Equal(200, (await SendAsync(handler, "/health", IPAddress.Loopback)).Status);
    }

    [Fact]
    public async Task Without_a_token_another_host_is_refused()
    {
        var handler = Handler(token: null);

        Assert.Equal(403, (await SendAsync(handler, "/health", Remote, query: "?token=")).Status);
    }

    [Fact]
    public async Task A_host_list_is_refused_before_anything_is_forwarded()
    {
        var handler = Handler(token: null);

        var (status, body) = await SendAsync(
            handler,
            "/v1/messages",
            IPAddress.Loopback,
            host: "a.example,b.example"
        );

        Assert.Equal(400, status);
        Assert.Contains("invalid_request_error", body, StringComparison.Ordinal);
    }

    private static ProxyHandler Handler(string? token) =>
        new(
            new ProxyOptions(new RtokConfig()) { AccessToken = token, CertFile = null },
            new HttpClient(new RefusingHandler()),
            new LiveCalls()
        );

    private static async Task<(int Status, string Body)> SendAsync(
        ProxyHandler handler,
        string path,
        IPAddress peer,
        string query = "",
        string? authorization = null,
        string host = "127.0.0.1:8790"
    )
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Request.Headers.Host = host;
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }
        context.Connection.RemoteIpAddress = peer;
        using var body = new MemoryStream();
        context.Response.Body = body;

        await handler.HandleAsync(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    /// <summary>An upstream the access checks must never reach.</summary>
    private sealed class RefusingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException("forwarded");
    }
}
