using System.Net;
using System.Net.WebSockets;

namespace Rtok.Web.Tests;

public sealed class GuardTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    public async Task A_mismatched_origin_gets_403(string target)
    {
        await using var web = await WebFixture.StartAsync();

        Assert.Equal(403, await web.RawStatusAsync(target, origin: "http://evil.example"));
        Assert.Equal(
            403,
            await web.RawStatusAsync(
                target,
                origin: $"http://127.0.0.1:{web.Server.Address.Port + 1}"
            )
        );
        Assert.Equal(
            200,
            await web.RawStatusAsync(target, origin: $"http://{web.Server.Address.Authority}")
        );
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("rebind.attacker.test:3333")]
    [InlineData("")]
    public async Task A_host_that_is_not_an_ip_or_localhost_is_refused(string host)
    {
        await using var web = await WebFixture.StartAsync();

        Assert.Equal(403, await web.RawStatusAsync("/health", host: host));
        Assert.Equal(403, await web.RawStatusAsync("/", host: host));
        Assert.Equal(
            200,
            await web.RawStatusAsync("/health", host: $"localhost:{web.Server.Address.Port}")
        );
    }

    [Fact]
    public async Task The_socket_is_refused_on_a_bad_origin_or_host()
    {
        await using var web = await WebFixture.StartAsync();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            await RefusedAsync(
                web.Socket(),
                s => s.Options.SetRequestHeader("Origin", "http://evil.example")
            )
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            await RefusedAsync(
                web.Socket(),
                s => s.Options.SetRequestHeader("Host", "evil.example")
            )
        );
    }

    [Theory]
    [InlineData("127.0.0.1:3333", true)]
    [InlineData("[::1]:3333", true)]
    [InlineData("localhost", true)]
    [InlineData("app.localhost:80", true)]
    [InlineData("192.168.1.10:3333", true)]
    [InlineData("example.com", false)]
    [InlineData("localhost.example.com", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void The_host_check(string? host, bool allowed) =>
        Assert.Equal(allowed, RequestGuard.HostAllowed(host));

    [Theory]
    [InlineData(null, "127.0.0.1:3333", true)]
    [InlineData("http://127.0.0.1:3333", "127.0.0.1:3333", true)]
    [InlineData("http://localhost", "localhost:80", true)]
    [InlineData("http://127.0.0.1:3334", "127.0.0.1:3333", false)]
    [InlineData("http://evil.example", "127.0.0.1:3333", false)]
    [InlineData("null", "127.0.0.1:3333", false)]
    [InlineData("http://127.0.0.1:3333, http://evil.example", "127.0.0.1:3333", false)]
    public void The_origin_check(string? origin, string host, bool allowed) =>
        Assert.Equal(allowed, RequestGuard.OriginAllowed(origin, host));

    internal static async Task<HttpStatusCode> RefusedAsync(
        Uri uri,
        Action<ClientWebSocket>? configure = null
    )
    {
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        configure?.Invoke(socket);
        await Assert.ThrowsAsync<WebSocketException>(() =>
            socket.ConnectAsync(uri, TestContext.Current.CancellationToken)
        );
        return socket.HttpStatusCode;
    }
}
