using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rtok.Model;
using Rtok.Web.Contracts;

namespace Rtok.Web;

/// <summary>
/// <c>rtok web</c>: Kestrel serving <c>/health</c>, <c>/ws</c> and the embedded client (N13).
/// Every request passes the Host and Origin checks; <c>/ws</c> also needs this run's token.
/// The snapshot is the operator model's, rebuilt off-thread each tick.
/// </summary>
public sealed class WebServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly OperatorModel _model;
    private readonly SnapshotFeed _feed;
    private readonly WsDispatcher _dispatcher;
    private readonly ITimer _tick;
    private readonly string _host;

    private WebServer(
        WebApplication app,
        OperatorModel model,
        SnapshotFeed feed,
        WebOptions options,
        string? token
    )
    {
        _app = app;
        _model = model;
        _feed = feed;
        _dispatcher = new WsDispatcher(model, options.Expander);
        _host = options.Host;
        Token = token;
        _tick = TimeProvider.System.CreateTimer(
            _ => _model.Refresh(),
            null,
            TimeSpan.Zero,
            options.Tick
        );
    }

    /// <summary>The token <c>/ws</c> requires; null with <see cref="WebOptions.NoToken"/>.</summary>
    public string? Token { get; }

    /// <summary>The bound address, such as <c>http://127.0.0.1:3333/</c>.</summary>
    public Uri Address { get; private set; } = new("http://127.0.0.1/");

    /// <summary>The address to open: <see cref="Address"/> with the token.</summary>
    public Uri OpenAddress =>
        Token is null ? Address : new Uri(Address, "?token=" + Uri.EscapeDataString(Token));

    /// <summary>Checks the options, binds and starts serving.</summary>
    /// <param name="options">Where to bind and what to serve.</param>
    /// <param name="sources">The operator model's page sources.</param>
    /// <param name="pluginIds">The linked plugins, the only ones a toggle may touch.</param>
    /// <param name="config">The config environment writes are checked in.</param>
    /// <param name="cancellationToken">Stops the start.</param>
    /// <returns>The running server.</returns>
    /// <exception cref="WebStartException">The host is refused, or the port cannot be bound.</exception>
    public static async Task<WebServer> StartAsync(
        WebOptions options,
        ModelSources sources,
        IEnumerable<string> pluginIds,
        Rtok.Config.ConfigLoadOptions? config = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var address = BindAddress(options);
        var token = options.NoToken ? null : WebToken.New();
        var feed = new SnapshotFeed();
        var model = new OperatorModel(sources, pluginIds, config, built: feed.Publish);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseKestrelCore().ConfigureKestrel(k => k.Listen(address, options.Port));
        builder.Services.AddRoutingCore();
        var app = builder.Build();
        var server = new WebServer(app, model, feed, options, token);
        server.Map(options.Files);
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException e)
        {
            await server.DisposeAsync().ConfigureAwait(false);
            throw new WebStartException(
                $"rtok web: cannot bind {options.Host}:{options.Port}: {e.Message}",
                e
            );
        }
        server.Address = server.Bound();
        return server;
    }

    /// <summary>Serves until <paramref name="cancellationToken"/> fires.</summary>
    /// <param name="cancellationToken">Ctrl-C.</param>
    /// <returns>A task that completes when the server stopped.</returns>
    public Task WaitAsync(CancellationToken cancellationToken) =>
        _app.WaitForShutdownAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _tick.DisposeAsync().ConfigureAwait(false);
        await _app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    private static IPAddress BindAddress(WebOptions options)
    {
        var host = options.Host.Trim('[', ']');
        IPAddress? address =
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                ? IPAddress.Loopback
            : IPAddress.TryParse(host, out var ip) ? ip
            : null;
        if (address is null)
        {
            throw new WebStartException(
                $"rtok web: web.host must be an IP address or localhost, not {options.Host}"
            );
        }
        if (options.NoToken && !RequestGuard.IsLoopback(host))
        {
            throw new WebStartException(
                $"rtok web: refusing to serve {options.Host} without the token: anyone who can "
                    + "reach the port could change config. Drop --no-token, or bind 127.0.0.1"
            );
        }
        return address;
    }

    private Uri Bound()
    {
        var addresses = _app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        var first =
            addresses?.Addresses.FirstOrDefault()
            ?? throw new WebStartException("rtok web: no address bound");
        return new Uri(first.EndsWith('/') ? first : first + "/");
    }

    private void Map(ClientFiles files)
    {
        var assets = new AssetServer(files);
        _app.Use(GuardAsync);
        _app.UseWebSockets();
        _app.UseRouting();
        _app.MapGet("/health", HealthAsync);
        _app.Map("/ws", SocketAsync);
        // MapFallback alone matches {*path:nonfile}: the assets themselves would 404.
        _app.MapFallback("{*path}", assets.ServeAsync);
    }

    private static Task GuardAsync(HttpContext context, RequestDelegate next)
    {
        var headers = context.Request.Headers;
        var host = headers.Host.ToString();
        if (
            !RequestGuard.HostAllowed(host)
            || !RequestGuard.OriginAllowed(Single(headers.Origin), host)
        )
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        return next(context);
    }

    private static string? Single(Microsoft.Extensions.Primitives.StringValues values) =>
        values.Count == 0 ? null : values.ToString();

    private Task HealthAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(
                new Health(true, _host, Address.Port),
                WebJsonContext.Default.Health
            ),
            context.RequestAborted
        );
    }

    private async Task SocketAsync(HttpContext context)
    {
        if (
            Token is not null
            && !WebToken.Matches(
                Token,
                context.Request.Query["token"].ToString() is { Length: > 0 } t ? t : null
            )
        )
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await RunAsync(socket, context.RequestAborted).ConfigureAwait(false);
    }

    private async Task RunAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var (snapshot, seen) = await _feed
            .NextAsync(0)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await SendAsync(socket, new SnapshotFrame(snapshot), cancellationToken)
            .ConfigureAwait(false);
        var receive = ReceiveAsync(socket, cancellationToken);
        while (true)
        {
            var next = _feed.NextAsync(seen);
            if (await Task.WhenAny(receive, next).ConfigureAwait(false) == next)
            {
                (snapshot, seen) = await next.ConfigureAwait(false);
                await SendAsync(socket, new SnapshotFrame(snapshot), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }
            var text = await receive.ConfigureAwait(false);
            if (text is null)
            {
                break;
            }
            var answer =
                text.Length == 0
                    ? new MessageFrame($"a message is at most {WsDispatcher.MaxMessageBytes} bytes")
                    : await Task.Run(() => _dispatcher.Answer(text), cancellationToken)
                        .ConfigureAwait(false);
            await SendAsync(socket, answer, cancellationToken).ConfigureAwait(false);
            receive = ReceiveAsync(socket, cancellationToken);
        }
        if (socket.State == WebSocketState.CloseReceived)
        {
            await socket
                .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static Task SendAsync(
        WebSocket socket,
        ServerFrame frame,
        CancellationToken cancellationToken
    ) =>
        socket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(frame, WsJsonContext.Default.ServerFrame),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken
        );

    // The next whole text message; null when the socket closed, empty when it was too big
    // (the rest is read and dropped).
    private static async Task<string?> ReceiveAsync(
        WebSocket socket,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        var tooBig = false;
        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException)
            {
                return null;
            }
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            if (!tooBig && message.Length + result.Count <= WsDispatcher.MaxMessageBytes)
            {
                message.Write(buffer, 0, result.Count);
            }
            else
            {
                tooBig = true;
            }
            if (result.EndOfMessage)
            {
                return tooBig
                    ? ""
                    : Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }
        }
    }
}
