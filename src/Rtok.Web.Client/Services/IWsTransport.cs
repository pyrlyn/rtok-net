namespace Rtok.Web.Client.Services;

/// <summary>One WebSocket to the host: text frames in and out. The browser's socket in the app,
/// a scripted one in tests.</summary>
public interface IWsTransport : IAsyncDisposable
{
    /// <summary>Opens the socket.</summary>
    /// <param name="uri">The <c>ws://</c> or <c>wss://</c> address with the token.</param>
    /// <param name="cancellationToken">Stops the attempt.</param>
    /// <returns>A task that completes when the socket is open.</returns>
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>Sends one text frame.</summary>
    /// <param name="text">The frame.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes when the frame is sent.</returns>
    Task SendAsync(string text, CancellationToken cancellationToken);

    /// <summary>Receives the next whole text frame.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The frame, or null when the socket closed.</returns>
    Task<string?> ReceiveAsync(CancellationToken cancellationToken);
}
