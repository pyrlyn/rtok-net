using System.Text.Json;
using System.Threading.Channels;
using Rtok.Web.Client.Services;
using Rtok.Web.Contracts;

namespace Rtok.Web.Client.Tests;

/// <summary>A socket the test drives: it pushes frames and closes; the client's sends are kept.</summary>
internal sealed class ScriptedTransport : IWsTransport
{
    private Channel<string?> _frames = Channel.CreateUnbounded<string?>();

    public List<Uri> Connects { get; } = [];

    public List<string> Sent { get; } = [];

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        lock (Connects)
        {
            Connects.Add(uri);
            _frames = Channel.CreateUnbounded<string?>();
        }
        return Task.CompletedTask;
    }

    public Task SendAsync(string text, CancellationToken cancellationToken)
    {
        lock (Sent)
        {
            Sent.Add(text);
        }
        return Task.CompletedTask;
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        Channel<string?> frames;
        lock (Connects)
        {
            frames = _frames;
        }
        return await frames.Reader.ReadAsync(cancellationToken);
    }

    public void Push(ServerFrame frame) =>
        _frames.Writer.TryWrite(JsonSerializer.Serialize(frame, WsJsonContext.Default.ServerFrame));

    /// <summary>The host drops the socket.</summary>
    public void Close() => _frames.Writer.TryWrite(null);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public static Snapshot Snapshot(long input, params SavingsDay[] savings)
    {
        var page = new TextPage("text\n", Ready: true);
        return new Snapshot(
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new OverviewPage(input, 0, 0, 7, 1, [], [], savings),
            [],
            [],
            [],
            page,
            page,
            page,
            page,
            [],
            page,
            page,
            page,
            page,
            page,
            page
        );
    }
}
