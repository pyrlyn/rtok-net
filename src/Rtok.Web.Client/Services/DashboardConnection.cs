using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Rtok.Web.Contracts;

namespace Rtok.Web.Client.Services;

/// <summary>
/// The dashboard's one socket to <c>rtok web</c>: connects with the run's token, keeps the last
/// snapshot, answers each request with the frame that echoes its id, and goes offline (with
/// Reconnect) when the socket drops. Pages read <see cref="Snapshot"/>, never the host.
/// </summary>
public sealed class DashboardConnection : IAsyncDisposable
{
    /// <summary>The sessionStorage key that keeps the run's token across reloads.</summary>
    public const string TokenKey = "rtok-token";

    private readonly IWsTransport _transport;
    private readonly NavigationManager _navigation;
    private readonly IJSRuntime _js;
    private readonly Dictionary<long, TaskCompletionSource<ServerFrame>> _pending = [];
    private readonly Lock _lock = new();
    private CancellationTokenSource? _run;
    private string? _token;
    private bool _started;
    private long _next;

    /// <summary>Creates the connection; nothing happens until <see cref="StartAsync"/>.</summary>
    /// <param name="transport">The socket.</param>
    /// <param name="navigation">Where the page was loaded from: the host and the token.</param>
    /// <param name="js">sessionStorage, for the token.</param>
    public DashboardConnection(IWsTransport transport, NavigationManager navigation, IJSRuntime js)
    {
        _transport = transport;
        _navigation = navigation;
        _js = js;
    }

    /// <summary>Raised after every change of state, snapshot or notice.</summary>
    public event EventHandler? Changed;

    /// <summary>Where the socket stands.</summary>
    public ConnectionState State { get; private set; } = ConnectionState.Loading;

    /// <summary>The last snapshot; kept while offline so the page still shows it.</summary>
    public Snapshot? Snapshot { get; private set; }

    /// <summary>The last notice: why the socket is offline, or a refused request.</summary>
    public string? Notice { get; private set; }

    /// <summary>Connects once; later calls do nothing.</summary>
    /// <returns>A task that completes when the socket is open or found offline.</returns>
    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }
        _started = true;
        _token = await TokenAsync().ConfigureAwait(false);
        await ConnectAsync().ConfigureAwait(false);
    }

    /// <summary>Drops the socket, if any, and connects again.</summary>
    /// <returns>A task that completes when the socket is open or found offline.</returns>
    public Task ReconnectAsync() => ConnectAsync();

    /// <summary>Sends one request and waits for the frame that answers it.</summary>
    /// <param name="request">The request; its id is set here.</param>
    /// <returns>The answer, or a message frame when the socket is down.</returns>
    public async Task<ServerFrame> SendAsync(WsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var answer = new TaskCompletionSource<ServerFrame>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        long req;
        lock (_lock)
        {
            req = ++_next;
            _pending[req] = answer;
        }
        try
        {
            var text = JsonSerializer.Serialize(
                request with
                {
                    Req = req,
                },
                WsJsonContext.Default.WsRequest
            );
            await _transport.SendAsync(text, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is WebSocketException or InvalidOperationException)
        {
            Answer(req, new MessageFrame("rtok is offline") { Req = req });
        }
        return await answer.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await (_run?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        _run?.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ConnectAsync()
    {
        if (_run is not null)
        {
            await _run.CancelAsync().ConfigureAwait(false);
            _run.Dispose();
        }
        var run = new CancellationTokenSource();
        _run = run;
        Set(ConnectionState.Loading, notice: null);
        try
        {
            await _transport.ConnectAsync(SocketUri(), run.Token).ConfigureAwait(false);
        }
        catch (Exception e)
            when (e is WebSocketException or InvalidOperationException or OperationCanceledException
            )
        {
            if (!run.IsCancellationRequested)
            {
                GoOffline("cannot reach rtok web");
            }
            return;
        }
        _ = ReceiveAsync(run.Token);
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (
                await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } text
            )
            {
                Handle(text);
            }
        }
        catch (Exception e)
            when (e is WebSocketException or InvalidOperationException or OperationCanceledException
            )
        {
            // Dropped; the state below says so.
        }
        if (!cancellationToken.IsCancellationRequested)
        {
            GoOffline("the connection to rtok closed");
        }
    }

    private void Handle(string text)
    {
        ServerFrame? frame;
        try
        {
            frame = JsonSerializer.Deserialize(text, WsJsonContext.Default.ServerFrame);
        }
        catch (JsonException)
        {
            return;
        }
        switch (frame)
        {
            case SnapshotFrame snapshot:
                Snapshot = snapshot.Snapshot;
                Set(ConnectionState.Live, Notice);
                break;
            case MessageFrame { Req: null } message:
                Set(State, message.Text);
                break;
        }
        if (frame?.Req is { } req)
        {
            Answer(req, frame);
        }
    }

    private void Answer(long req, ServerFrame frame)
    {
        TaskCompletionSource<ServerFrame>? answer;
        lock (_lock)
        {
            _pending.Remove(req, out answer);
        }
        answer?.TrySetResult(frame);
    }

    private void GoOffline(string notice)
    {
        List<KeyValuePair<long, TaskCompletionSource<ServerFrame>>> waiting;
        lock (_lock)
        {
            waiting = [.. _pending];
            _pending.Clear();
        }
        foreach (var (req, answer) in waiting)
        {
            answer.TrySetResult(new MessageFrame("rtok is offline") { Req = req });
        }
        Set(ConnectionState.Offline, notice);
    }

    private void Set(ConnectionState state, string? notice)
    {
        State = state;
        Notice = notice;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Uri SocketUri()
    {
        var page = new Uri(_navigation.BaseUri);
        var socket = new UriBuilder(page)
        {
            Scheme = string.Equals(page.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                ? "wss"
                : "ws",
            Path = page.AbsolutePath.TrimEnd('/') + "/ws",
            Query = _token is null ? "" : "token=" + Uri.EscapeDataString(_token),
        };
        return socket.Uri;
    }

    // The token arrives once in the address rtok web printed; it moves to sessionStorage and
    // out of the address bar, so a reload or a link to another page still connects.
    private async Task<string?> TokenAsync()
    {
        var uri = new Uri(_navigation.Uri);
        var token = QueryValue(uri.Query, "token");
        if (token is not null)
        {
            await TryJsAsync("sessionStorage.setItem", TokenKey, token).ConfigureAwait(false);
            var rest = RemoveQueryValue(uri.Query, "token");
            _navigation.NavigateTo(uri.GetLeftPart(UriPartial.Path) + rest, replace: true);
            return token;
        }
        try
        {
            return await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task TryJsAsync(string identifier, params object?[] args)
    {
        try
        {
            await _js.InvokeVoidAsync(identifier, args).ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Storage blocked: the token lives only in this page.
        }
    }

    internal static string? QueryValue(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            var key = eq < 0 ? pair : pair[..eq];
            if (string.Equals(key, name, StringComparison.Ordinal))
            {
                return eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }
        return null;
    }

    internal static string RemoveQueryValue(string query, string name)
    {
        var kept = query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !string.Equals(pair.Split('=')[0], name, StringComparison.Ordinal))
            .ToList();
        return kept.Count == 0 ? "" : "?" + string.Join('&', kept);
    }
}
