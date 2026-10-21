using System.Buffers.Binary;
using System.Net.Sockets;
using Rtok.Hook.Contracts;

namespace Rtok.Smoke;

/// <summary>
/// A stand-in resident on a home's socket: it takes one connection, records the request and
/// answers with what <c>reply</c> returns, or hangs up without a word when that is null.
/// </summary>
internal sealed class FakeResident : IDisposable
{
    private readonly Socket _listener = new(
        AddressFamily.Unix,
        SocketType.Stream,
        ProtocolType.Unspecified
    );
    private readonly TaskCompletionSource<HookRequest?> _request = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    private FakeResident(string home, Func<HookRequest?, byte[]?> reply)
    {
        var endpoint = HookEndpoint.For(home)!;
        HookEndpoint.EnsurePrivateDir(endpoint.RunDir);
        _listener.Bind(new UnixDomainSocketEndPoint(endpoint.Address));
        _listener.Listen(16);
        _ = ServeAsync(reply);
    }

    /// <summary>The request the client sent (null when it was malformed); pending until one arrives.</summary>
    public Task<HookRequest?> Request => _request.Task;

    public static FakeResident Start(string home, Func<HookRequest?, byte[]?> reply) =>
        new(home, reply);

    private async Task ServeAsync(Func<HookRequest?, byte[]?> reply)
    {
        try
        {
            using var peer = await _listener.AcceptAsync().ConfigureAwait(false);
            await using var stream = new NetworkStream(peer);
            var length = new byte[4];
            await stream.ReadExactlyAsync(length).ConfigureAwait(false);
            var body = new byte[BinaryPrimitives.ReadUInt32LittleEndian(length)];
            await stream.ReadExactlyAsync(body).ConfigureAwait(false);
            var request = HookWire.DecodeRequest(body);
            _request.TrySetResult(request);
            if (reply(request) is { } answer)
            {
                await stream.WriteAsync(HookWire.Frame(answer)).ConfigureAwait(false);
            }
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    public void Dispose() => _listener.Dispose();
}
