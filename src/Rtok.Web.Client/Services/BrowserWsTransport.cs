using System.Net.WebSockets;
using System.Text;

namespace Rtok.Web.Client.Services;

/// <summary>The browser's WebSocket.</summary>
internal sealed class BrowserWsTransport : IWsTransport
{
    private ClientWebSocket? _socket;

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        _socket?.Dispose();
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    public Task SendAsync(string text, CancellationToken cancellationToken) =>
        Socket()
            .SendAsync(
                Encoding.UTF8.GetBytes(text),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken
            );

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var socket = Socket();
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket
                .ReceiveAsync(buffer.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _socket?.Dispose();
        _socket = null;
        return ValueTask.CompletedTask;
    }

    private ClientWebSocket Socket() =>
        _socket ?? throw new InvalidOperationException("the socket is not open");
}
