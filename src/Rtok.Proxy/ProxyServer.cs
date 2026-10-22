using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Rtok.Config;

namespace Rtok.Proxy;

/// <summary><c>rtok proxy</c>: Kestrel slim on <c>[proxy] bind:port</c> with one request
/// delegate, no routing or MVC.</summary>
public sealed class ProxyServer : IAsyncDisposable
{
    /// <summary>A black-holed upstream answers 502 after this, not after the read budget.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private readonly ProxyHandler _handler;

    private ProxyServer(WebApplication app, HttpClient client, ProxyHandler handler, Uri address)
    {
        _app = app;
        _client = client;
        _handler = handler;
        Live = handler.Live;
        Address = address;
    }

    /// <summary>The address it listens on, with the real port when the config asked for 0.</summary>
    public Uri Address { get; }

    /// <summary>The plain-mode requests <c>/live</c> shows.</summary>
    public LiveCalls Live { get; }

    /// <summary>Binds and starts serving.</summary>
    /// <param name="options">Config, store, log and TLS.</param>
    /// <param name="cancellationToken">Stops a start in progress.</param>
    /// <returns>The running proxy.</returns>
    /// <exception cref="ProxyTlsException">The <c>SSL_CERT_FILE</c> bundle cannot be used.</exception>
    /// <exception cref="FormatException"><c>[proxy] bind</c> is not an address.</exception>
    public static async Task<ProxyServer> StartAsync(
        ProxyOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var endpoint = Endpoint(options.Config.Proxy);
        var client = new HttpClient(
            options.Upstream ?? UpstreamHandler(options),
            options.Upstream is null
        )
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder
            .WebHost.UseKestrelCore()
            .ConfigureKestrel(kestrel => Configure(kestrel, endpoint, options.Config.Proxy.Port));
        var app = builder.Build();
        var live = new LiveCalls();
        var handler = new ProxyHandler(options, client, live);
        app.Run(handler.HandleAsync);
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(false);
            handler.Dispose();
            client.Dispose();
            throw;
        }
        var address = app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        return new ProxyServer(app, client, handler, new Uri(address));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _handler.Dispose();
        _client.Dispose();
    }

    /// <summary>Where <c>[proxy] bind</c> and <c>port</c> listen: an IP address, or null for
    /// <c>localhost</c> (both loopback addresses).</summary>
    /// <param name="proxy">The <c>[proxy]</c> section.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="FormatException">The bind is neither.</exception>
    internal static IPEndPoint? Endpoint(ProxySection proxy)
    {
        var bind = proxy.Bind.Trim();
        if (bind.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return IPAddress.TryParse(bind, out var ip)
            ? new IPEndPoint(ip, proxy.Port)
            : throw new FormatException($"[proxy] bind is not an IP address or localhost: {bind}");
    }

    private static void Configure(KestrelServerOptions kestrel, IPEndPoint? endpoint, int port)
    {
        kestrel.AddServerHeader = false;
        kestrel.Limits.MaxRequestBodySize = ProxyOptions.MaxBodyBytes;
        if (endpoint is null)
        {
            kestrel.ListenLocalhost(port);
        }
        else
        {
            kestrel.Listen(endpoint);
        }
    }

    /// <summary>The upstream connection: no redirects, cookies or decompression (the bytes pass
    /// as they come), a connect deadline, and the <c>SSL_CERT_FILE</c> roots when set.</summary>
    private static SocketsHttpHandler UpstreamHandler(ProxyOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = ConnectTimeout,
        };
        if (options.CertFile is { Length: > 0 } file)
        {
            try
            {
                ProxyTls.Trust(handler, ProxyTls.LoadBundle(file));
            }
            catch
            {
                handler.Dispose();
                throw;
            }
        }
        return handler;
    }
}
