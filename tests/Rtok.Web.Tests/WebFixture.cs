using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Rtok.Archive;
using Rtok.Config;

namespace Rtok.Web.Tests;

/// <summary><c>rtok web</c> on a free loopback port over the model fixture's home and store.</summary>
internal sealed class WebFixture : IAsyncDisposable
{
    private WebFixture(ModelFixture model, WebServer server)
    {
        Model = model;
        Server = server;
        Http = new HttpClient { BaseAddress = server.Address };
    }

    public ModelFixture Model { get; }

    public WebServer Server { get; }

    public HttpClient Http { get; }

    public static async Task<WebFixture> StartAsync(
        Func<ModelSources, ModelSources>? sources = null,
        ClientFiles? files = null
    )
    {
        var model = new ModelFixture();
        try
        {
            var server = await WebServer
                .StartAsync(
                    new WebOptions("127.0.0.1", 0, files ?? ClientFiles.Embedded())
                    {
                        Tick = TimeSpan.FromMilliseconds(100),
                        Expander = ExpanderOf(model),
                    },
                    (sources ?? (s => s))(model.Sources()),
                    ["measure", "docs"],
                    model.Options,
                    TestContext.Current.CancellationToken
                )
                .ConfigureAwait(false);
            return new WebFixture(model, server);
        }
        catch
        {
            model.Dispose();
            throw;
        }
    }

    /// <summary>The expand handler over <paramref name="model"/>'s store and config.</summary>
    public static Expander ExpanderOf(ModelFixture model) =>
        new(model.Store, ConfigLoader.Load(model.Options).Config, "web");

    /// <summary>The <c>/ws</c> address with <paramref name="token"/>, this run's by default.</summary>
    public Uri Socket(string? token = null)
    {
        var ws = new UriBuilder(Server.Address) { Scheme = "ws", Path = "/ws" };
        ws.Query = "token=" + Uri.EscapeDataString(token ?? Server.Token!);
        return ws.Uri;
    }

    /// <summary>A socket connected with this run's token.</summary>
    public async Task<ClientWebSocket> ConnectAsync()
    {
        var socket = new ClientWebSocket();
        await socket
            .ConnectAsync(Socket(), TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
        return socket;
    }

    /// <summary>The status code of a request sent byte for byte, so nothing normalizes the
    /// target or the headers on the way.</summary>
    public async Task<int> RawStatusAsync(string target, string? host = null, string? origin = null)
    {
        using var client = new TcpClient();
        await client
            .ConnectAsync(
                Server.Address.Host,
                Server.Address.Port,
                TestContext.Current.CancellationToken
            )
            .ConfigureAwait(false);
        var stream = client.GetStream();
        var request = new StringBuilder()
            .Append("GET ")
            .Append(target)
            .Append(" HTTP/1.1\r\nHost: ")
            .Append(host ?? Server.Address.Authority)
            .Append("\r\n");
        if (origin is not null)
        {
            request.Append("Origin: ").Append(origin).Append("\r\n");
        }
        request.Append("Connection: close\r\n\r\n");
        await stream
            .WriteAsync(
                Encoding.ASCII.GetBytes(request.ToString()),
                TestContext.Current.CancellationToken
            )
            .ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var status = await reader
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
        return int.Parse(status!.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await Server.DisposeAsync().ConfigureAwait(false);
        Model.Dispose();
    }

    /// <summary>Sends one text message.</summary>
    public static Task SendAsync(ClientWebSocket socket, string text) =>
        socket.SendAsync(
            Encoding.UTF8.GetBytes(text),
            WebSocketMessageType.Text,
            endOfMessage: true,
            TestContext.Current.CancellationToken
        );

    /// <summary>The next frame, parsed with the shared context.</summary>
    public static async Task<ServerFrame> ReceiveAsync(ClientWebSocket socket)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var message = new MemoryStream();
        var buffer = new byte[16 * 1024];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, timeout.Token).ConfigureAwait(false);
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonSerializer.Deserialize(message.ToArray(), WsJsonContext.Default.ServerFrame)!;
    }

    /// <summary>The next frame that is not a pushed snapshot.</summary>
    public static async Task<ServerFrame> ReplyAsync(ClientWebSocket socket)
    {
        while (true)
        {
            var frame = await ReceiveAsync(socket).ConfigureAwait(false);
            if (frame is not SnapshotFrame)
            {
                return frame;
            }
        }
    }
}
