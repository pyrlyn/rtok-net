using System.Net;
using Rtok.Config;
using Rtok.Core;

namespace Rtok.Proxy.Tests;

public sealed class AddressTests
{
    [Theory]
    [InlineData(
        "https://api.anthropic.com",
        "/v1/messages",
        null,
        "https://api.anthropic.com/v1/messages"
    )]
    [InlineData(
        "https://gw.example/anthropic/",
        "/v1/messages",
        "beta=true",
        "https://gw.example/anthropic/v1/messages?beta=true"
    )]
    [InlineData(
        "https://gw.example/base?key=k",
        "/v1/messages",
        null,
        "https://gw.example/base/v1/messages?key=k"
    )]
    [InlineData(
        "https://gw.example/base?key=k",
        "/v1/messages",
        "a=1",
        "https://gw.example/base/v1/messages?a=1"
    )]
    [InlineData(
        "http://127.0.0.1:9000",
        "//v1//files/a%2Fb",
        "",
        "http://127.0.0.1:9000/v1/files/a%2Fb?"
    )]
    [InlineData("http://127.0.0.1:9000", "/", null, "http://127.0.0.1:9000/")]
    public void Upstream_joins_base_path_and_query(
        string baseUrl,
        string path,
        string? query,
        string joined
    ) => Assert.Equal(joined, UpstreamUrl.Join(baseUrl, path, query));

    [Theory]
    [InlineData("")]
    [InlineData("api.anthropic.com")]
    [InlineData("ftp://host/x")]
    public void Upstream_must_be_http(string baseUrl) =>
        Assert.Throws<FormatException>(() => UpstreamUrl.Join(baseUrl, "/v1/messages", null));

    [Theory]
    [InlineData(null, 0)]
    [InlineData("1", 1)]
    [InlineData(" 7 ", 7)]
    [InlineData("yes", 1)]
    [InlineData("-3", 1)]
    [InlineData("99999999999", 1)]
    public void Hops_count_what_another_rtok_sent(string? header, int hops) =>
        Assert.Equal(hops, Hops.Count(header));

    [Theory]
    [InlineData("127.0.0.1", "http://127.0.0.1:8790")]
    [InlineData("0.0.0.0", "http://127.0.0.1:8790")]
    [InlineData("::", "http://[::1]:8790")]
    [InlineData("::1", "http://[::1]:8790")]
    [InlineData("10.1.2.3", "http://10.1.2.3:8790")]
    [InlineData("localhost", "http://localhost:8790")]
    public void Clients_dial_the_bind_or_loopback(string bind, string root)
    {
        var proxy = new ProxySection { Bind = bind };

        Assert.Equal(root, ProxyUrls.Root(proxy));
        Assert.Equal(root, ProxyUrls.Anthropic(proxy));
        Assert.Equal(root + "/v1", ProxyUrls.OpenAi(proxy));
    }

    [Fact]
    public void The_default_bind_is_loopback_and_a_name_is_refused()
    {
        Assert.Equal(
            new IPEndPoint(IPAddress.Loopback, 8790),
            ProxyServer.Endpoint(new ProxySection())
        );
        Assert.Null(ProxyServer.Endpoint(new ProxySection { Bind = "LocalHost" }));
        Assert.Throws<FormatException>(() =>
            ProxyServer.Endpoint(new ProxySection { Bind = "proxy.example" })
        );
    }

    [Fact]
    public void Health_and_live_are_for_loopback_or_the_token()
    {
        var remote = IPAddress.Parse("10.0.0.5");

        Assert.True(ProxyHandler.Allowed(null, null, null));
        Assert.True(ProxyHandler.Allowed(IPAddress.Loopback, null, null));
        Assert.True(ProxyHandler.Allowed(IPAddress.IPv6Loopback, null, null));
        Assert.True(ProxyHandler.Allowed(IPAddress.Loopback.MapToIPv6(), null, null));
        Assert.False(ProxyHandler.Allowed(remote, null, null));
        Assert.False(ProxyHandler.Allowed(remote, "t", null));
        Assert.False(ProxyHandler.Allowed(remote, "wrong", "t"));
        Assert.True(ProxyHandler.Allowed(remote, "t", "t"));
    }

    [Fact]
    public void Access_tokens_are_long_random_and_read_from_bearer_or_query()
    {
        var token = AccessToken.New();

        Assert.True(token.Length >= 43);
        Assert.NotEqual(token, AccessToken.New(), StringComparer.Ordinal);
        Assert.True(AccessToken.Matches(token, token));
        Assert.False(AccessToken.Matches(token, token[..^1]));
        Assert.False(AccessToken.Matches(token, null));
        Assert.Equal("abc", AccessToken.Presented("Bearer abc", "q"));
        Assert.Null(AccessToken.Presented("Basic abc", "q"));
        Assert.Equal("q", AccessToken.Presented(null, "q"));
        Assert.Null(AccessToken.Presented("", ""));
    }
}
