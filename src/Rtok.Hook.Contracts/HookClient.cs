using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;

namespace Rtok.Hook.Contracts;

/// <summary>The client side of the wire: one request, one reply, within a budget.</summary>
public static class HookClient
{
    /// <summary>How long the client waits for the resident (catalog B1).</summary>
    public const int BudgetMs = 50;

    /// <summary>
    /// Sends <paramref name="body"/> (a request body) to the resident at <paramref name="endpoint"/>
    /// and reads its reply within <paramref name="budgetMs"/>; <paramref name="stdout"/> is the
    /// hook's output when <see cref="HookOutcome.Served"/>.
    /// </summary>
    public static HookOutcome Exchange(
        HookEndpoint endpoint,
        ReadOnlySpan<byte> body,
        out byte[] stdout,
        int budgetMs = BudgetMs
    )
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        stdout = [];
        var frame = HookWire.Frame(body);
        byte[]? reply;
        try
        {
            reply = OperatingSystem.IsWindows()
                ? ExchangePipe(endpoint.Address, frame, budgetMs)
                : ExchangeSocket(endpoint.Address, frame, budgetMs);
        }
        catch (Exception e)
            when (e
                    is IOException
                        or SocketException
                        or OperationCanceledException
                        or TimeoutException
            )
        {
            return HookOutcome.Failed;
        }
        if (reply is null)
        {
            return HookOutcome.Unreachable;
        }
        if (!HookWire.TryDecodeReply(reply, out var status, out var output))
        {
            return HookOutcome.Failed;
        }
        stdout = output.ToArray();
        return status switch
        {
            HookReplyStatus.Served => HookOutcome.Served,
            HookReplyStatus.RefusedVersion => HookOutcome.RefusedVersion,
            _ => HookOutcome.RefusedFingerprint,
        };
    }

    // Null when nothing listens.
    private static byte[]? ExchangeSocket(string path, byte[] frame, int budgetMs)
    {
        var start = Stopwatch.GetTimestamp();
        using var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified
        );
        // On Linux a full backlog blocks connect for up to the send timeout.
        socket.SendTimeout = budgetMs;
        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(path));
        }
        // A missing or stale socket; a busy resident (timed out) is alive, not absent.
        catch (SocketException e)
            when (e.SocketErrorCode is not (SocketError.TimedOut or SocketError.WouldBlock))
        {
            return null;
        }
        // The send timeout above bounds the send too: a request fits the socket buffer unless
        // it carries a large tool output.
        var sent = 0;
        while (sent < frame.Length)
        {
            sent += socket.Send(frame, sent, frame.Length - sent, SocketFlags.None);
        }
        var head = new byte[4];
        Receive(socket, head, start, budgetMs);
        var length = BinaryPrimitives.ReadInt32LittleEndian(head);
        if (length is < 0 or > HookWire.MaxFrame)
        {
            throw new IOException("bad reply length");
        }
        var reply = new byte[length];
        Receive(socket, reply, start, budgetMs);
        return reply;
    }

    private static void Receive(Socket socket, byte[] buf, long start, int budgetMs)
    {
        // Poll, not a receive timeout: macOS refuses setsockopt once the peer has closed, even
        // with the reply still buffered.
        var at = 0;
        while (at < buf.Length)
        {
            if (
                !socket.Poll(
                    TimeSpan.FromMilliseconds(Remaining(start, budgetMs)),
                    SelectMode.SelectRead
                )
            )
            {
                throw new TimeoutException();
            }
            var n = socket.Receive(buf, at, buf.Length - at, SocketFlags.None);
            if (n == 0)
            {
                throw new IOException("closed");
            }
            at += n;
        }
    }

    // A spent budget throws: a wait of 0 would not wait at all, a socket timeout of 0 forever.
    private static int Remaining(long start, int budgetMs)
    {
        var left = budgetMs - (int)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return left > 0 ? left : throw new TimeoutException();
    }

    private static byte[]? ExchangePipe(string name, byte[] frame, int budgetMs)
    {
        using var cts = new CancellationTokenSource(budgetMs);
        // CurrentUserOnly: the client checks that the pipe's owner is this user.
        using var pipe = new NamedPipeClientStream(
            ".",
            name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );
        try
        {
            pipe.Connect(0);
        }
        catch (TimeoutException)
        {
            return null;
        }
        pipe.WriteAsync(frame, cts.Token).AsTask().GetAwaiter().GetResult();
        var head = new byte[4];
        pipe.ReadExactlyAsync(head, cts.Token).AsTask().GetAwaiter().GetResult();
        var length = BinaryPrimitives.ReadInt32LittleEndian(head);
        if (length is < 0 or > HookWire.MaxFrame)
        {
            throw new IOException("bad reply length");
        }
        var reply = new byte[length];
        pipe.ReadExactlyAsync(reply, cts.Token).AsTask().GetAwaiter().GetResult();
        return reply;
    }
}
