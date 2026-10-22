using System.Net;

namespace Rtok.Web.Tests;

public sealed class StartTests
{
    [Fact]
    public async Task Health_answers_while_a_snapshot_build_is_blocked()
    {
        var logs = new CountingSource("log");
        logs.Gate.Reset();
        try
        {
            await using var web = await WebFixture.StartAsync(s => s with { Logs = logs });
            Assert.True(SpinWait.SpinUntil(() => logs.Reads > 0, TimeSpan.FromSeconds(10)));

            using var response = await web.Http.GetAsync(
                new Uri("health", UriKind.Relative),
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(
                $"{{\"ok\":true,\"host\":\"127.0.0.1\",\"port\":{web.Server.Address.Port}}}",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
            );
        }
        finally
        {
            logs.Gate.Set();
        }
    }

    [Fact]
    public async Task Port_zero_binds_a_free_port_and_the_open_address_carries_the_token()
    {
        await using var web = await WebFixture.StartAsync();

        Assert.NotEqual(0, web.Server.Address.Port);
        Assert.Equal("127.0.0.1", web.Server.Address.Host);
        Assert.Equal($"?token={web.Server.Token}", web.Server.OpenAddress.Query);
    }

    [Fact]
    public async Task Two_runs_have_different_tokens()
    {
        await using var one = await WebFixture.StartAsync();
        await using var two = await WebFixture.StartAsync();

        Assert.NotEqual(one.Server.Token, two.Server.Token, StringComparer.Ordinal);
        Assert.Equal(43, one.Server.Token!.Length); // 32 random bytes, base64url
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("192.168.1.10")]
    [InlineData("::")]
    public async Task A_non_loopback_host_without_the_token_is_refused_at_startup(string host)
    {
        using var model = new ModelFixture();

        var e = await Assert.ThrowsAsync<WebStartException>(() =>
            WebServer.StartAsync(
                new WebOptions(host, 0, ClientFiles.Embedded()) { NoToken = true },
                model.Sources(),
                ["measure", "docs"],
                model.Options,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("without the token", e.Message, StringComparison.Ordinal);
        Assert.Contains(host, e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("my-laptop")]
    public async Task A_host_name_is_refused_at_startup(string host)
    {
        using var model = new ModelFixture();

        var e = await Assert.ThrowsAsync<WebStartException>(() =>
            WebServer.StartAsync(
                new WebOptions(host, 0, ClientFiles.Embedded()),
                model.Sources(),
                ["measure", "docs"],
                model.Options,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("must be an IP address or localhost", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loopback_without_the_token_serves_the_socket()
    {
        using var model = new ModelFixture();
        var server = await WebServer.StartAsync(
            new WebOptions("localhost", 0, ClientFiles.Embedded()) { NoToken = true },
            model.Sources(),
            ["measure", "docs"],
            model.Options,
            TestContext.Current.CancellationToken
        );
        await using (server)
        {
            using var socket = new System.Net.WebSockets.ClientWebSocket();
            await socket.ConnectAsync(
                new Uri($"ws://{server.Address.Authority}/ws"),
                TestContext.Current.CancellationToken
            );

            Assert.IsType<SnapshotFrame>(await WebFixture.ReceiveAsync(socket));
            Assert.Null(server.Token);
            Assert.Equal(server.Address, server.OpenAddress);
        }
    }
}
