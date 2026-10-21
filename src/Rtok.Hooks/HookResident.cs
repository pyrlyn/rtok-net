using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using Rtok.Hook.Contracts;

namespace Rtok.Hooks;

/// <summary>
/// <c>rtok hook --serve</c> (N8): one process per home keeps the hook warm. It holds
/// <c>hook.lock</c>, listens on the socket bound inside the 0700 run directory (a pipe for this
/// user only on Windows), refuses a client of another version or fingerprint, and replays the
/// queue the client wrote while no resident answered.
/// </summary>
public static class HookResident
{
    public static ResidentExit Run(
        HookEndpoint endpoint,
        HookHandler handler,
        CancellationToken ct,
        ResidentOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(handler);
        options ??= new ResidentOptions();
        HookEndpoint.EnsurePrivateDir(endpoint.RunDir);
        using var lockFile = TakeLock(endpoint.LockPath, options.LockWait, ct);
        if (lockFile is null)
        {
            return ct.IsCancellationRequested
                ? ResidentExit.Cancelled
                : ResidentExit.AlreadyRunning;
        }
        lockFile.SetLength(0);
        lockFile.Write(
            Encoding.UTF8.GetBytes(
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "\n"
            )
        );
        lockFile.Flush();

        var server = new Server(endpoint, handler, options, ct);
        return server.Run();
    }

    private static FileStream? TakeLock(string path, TimeSpan wait, CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();
        while (true)
        {
            try
            {
                // FileShare.None takes an exclusive flock on Unix.
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
            }
            catch (IOException) when (Stopwatch.GetElapsedTime(start) < wait)
            {
                if (ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(50)))
                {
                    return null;
                }
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    private sealed class Server(
        HookEndpoint endpoint,
        HookHandler handler,
        ResidentOptions options,
        CancellationToken ct
    )
    {
        private readonly Lock _gate = new();
        private readonly CancellationTokenSource _stop =
            CancellationTokenSource.CreateLinkedTokenSource(ct);
        private int _versionRefused;

        public ResidentExit Run()
        {
            // _stop is not disposed: a connection still being served may cancel it.
            Replay();
            var listening = OperatingSystem.IsWindows() ? ListenPipe() : ListenSocket();
            var exit = ResidentExit.Cancelled;
            while (!_stop.Token.WaitHandle.WaitOne(options.Tick))
            {
                if (
                    !File.Exists(endpoint.LockPath)
                    || (!OperatingSystem.IsWindows() && !File.Exists(endpoint.Address))
                )
                {
                    exit = ResidentExit.LockLost;
                    break;
                }
                Replay();
            }
            _stop.Cancel();
            try
            {
                listening.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException) { }
            if (Volatile.Read(ref _versionRefused) == 1)
            {
                exit = ResidentExit.VersionRefused;
            }
            // Still holding the lock, the socket is ours, unless the lock vanished: then a new
            // resident may already have renamed its own into place.
            if (exit != ResidentExit.LockLost && !OperatingSystem.IsWindows())
            {
                File.Delete(endpoint.Address);
            }
            return exit;
        }

        private void Replay()
        {
            foreach (var body in HookBacklog.Drain(endpoint))
            {
                if (HookWire.DecodeRequest(body) is { } request && Serves(request))
                {
                    Handle(request, replay: true);
                }
            }
        }

        private bool Serves(HookRequest request) =>
            string.Equals(request.Version, options.Version, StringComparison.Ordinal)
            && request.Fingerprint == options.Fingerprint;

        private ReadOnlyMemory<byte> Handle(HookRequest request, bool replay)
        {
            lock (_gate)
            {
                try
                {
                    return handler(request, replay);
                }
#pragma warning disable CA1031 // Fail open: a failed call answers the no-op reply.
                catch (Exception)
#pragma warning restore CA1031
                {
                    return HookWire.NoOp.ToArray();
                }
            }
        }

        private byte[]? Answer(byte[] body)
        {
            if (HookWire.DecodeRequest(body) is not { } request)
            {
                return null;
            }
            if (!string.Equals(request.Version, options.Version, StringComparison.Ordinal))
            {
                // The resident stops once this reply is out (ServeAsync).
                Volatile.Write(ref _versionRefused, 1);
                return HookWire.EncodeReply(HookReplyStatus.RefusedVersion);
            }
            if (request.Fingerprint != options.Fingerprint)
            {
                return HookWire.EncodeReply(HookReplyStatus.RefusedFingerprint);
            }
            return HookWire.EncodeReply(
                HookReplyStatus.Served,
                Handle(request, replay: false).Span
            );
        }

        // Bound under a temporary name in the 0700 run directory, made 0600, then renamed into
        // place: no client ever sees a socket the umask left open (rtok S7).
        private Task ListenSocket()
        {
            var tmp = Path.Join(
                endpoint.RunDir,
                ".s" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
            );
            File.Delete(tmp);
            var listener = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified
            );
            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(tmp));
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                listener.Listen(128);
                File.Move(tmp, endpoint.Address, overwrite: true);
            }
            catch
            {
                listener.Dispose();
                File.Delete(tmp);
                throw;
            }
            return Task.Run(async () =>
            {
                using (listener)
                {
                    while (!_stop.IsCancellationRequested)
                    {
                        Socket client;
                        try
                        {
                            client = await listener.AcceptAsync(_stop.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (SocketException)
                        {
                            continue;
                        }
                        _ = Task.Run(() => ServeAsync(new NetworkStream(client, ownsSocket: true)));
                    }
                }
            });
        }

        // CurrentUserOnly: the pipe's ACL admits this user alone. FirstPipeInstance: a pipe of
        // that name someone else made first fails the start instead of being joined.
        private Task ListenPipe()
        {
            var first = new NamedPipeServerStream(
                endpoint.Address,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
                    | PipeOptions.CurrentUserOnly
                    | PipeOptions.FirstPipeInstance
            );
            return Task.Run(async () =>
            {
                var next = first;
                while (true)
                {
                    try
                    {
                        await next.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        await next.DisposeAsync().ConfigureAwait(false);
                        return;
                    }
                    var connected = next;
                    _ = Task.Run(() => ServeAsync(connected));
                    next = new NamedPipeServerStream(
                        endpoint.Address,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
                    );
                }
            });
        }

        private async Task ServeAsync(Stream stream)
        {
            await using (stream.ConfigureAwait(false))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(options.ReadTimeout);
                try
                {
                    var head = new byte[4];
                    await stream.ReadExactlyAsync(head, timeout.Token).ConfigureAwait(false);
                    var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(head);
                    if (length is < 0 or > HookWire.MaxFrame)
                    {
                        return;
                    }
                    var body = new byte[length];
                    await stream.ReadExactlyAsync(body, timeout.Token).ConfigureAwait(false);
                    if (Answer(body) is { } reply)
                    {
                        await stream
                            .WriteAsync(HookWire.Frame(reply), CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                    when (e is IOException or OperationCanceledException or SocketException)
                {
                    // The client gave up or went away; it already printed its no-op reply.
                }
                finally
                {
                    if (Volatile.Read(ref _versionRefused) == 1)
                    {
                        await _stop.CancelAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
