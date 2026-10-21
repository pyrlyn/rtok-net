using System.Buffers.Binary;
using System.Reflection;
using System.Text;

namespace Rtok.Hook.Contracts;

/// <summary>
/// The wire between <c>rtok-hook</c> and <c>rtok hook --serve</c> (ported from rtok's
/// <c>rtok-hook</c> crate). A frame is a little-endian <c>u32</c> length, then the body. A
/// request body is <c>version, fingerprint, event, host, cwd, host env, pid, stdin</c>: strings
/// and stdin length-prefixed, the fingerprint a <c>u64</c>, the pid a <c>u32</c>, the host env a
/// <c>u32</c> count of name/value pairs. A reply body is one <see cref="HookReplyStatus"/> byte
/// and, when served, the hook's stdout. A queued request is a request body in a file.
/// </summary>
public static class HookWire
{
    /// <summary>The largest body either end accepts: a PostToolUse payload carries a tool's output.</summary>
    public const int MaxFrame = 64 << 20;

    /// <summary>
    /// The default of <c>core.hook_max_input_bytes</c>. The client cannot load a config, so it reads
    /// at most this much stdin; a longer payload fails open.
    /// </summary>
    public const int MaxInput = 8 << 20;

    /// <summary>The no-op reply every host accepts.</summary>
    public static ReadOnlySpan<byte> NoOp => "{}"u8;

    /// <summary>This build's version, <c>&lt;version&gt;+&lt;commit&gt;</c>: both binaries carry this assembly.</summary>
    public static string Version { get; } =
        typeof(HookWire)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.0.0";

    /// <summary>The request body, without the frame length.</summary>
    public static byte[] EncodeRequest(HookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var size =
            Str(request.Version)
            + 8
            + Str(request.Event)
            + Str(request.Host)
            + Str(request.Cwd)
            + 4
            + request.HostEnv.Sum(kv => Str(kv.Key) + Str(kv.Value))
            + 4
            + 4
            + request.Stdin.Length;
        var body = new byte[size];
        var w = new Writer(body);
        w.String(request.Version);
        w.UInt64(request.Fingerprint);
        w.String(request.Event);
        w.String(request.Host);
        w.String(request.Cwd);
        w.UInt32((uint)request.HostEnv.Count);
        foreach (var (name, value) in request.HostEnv)
        {
            w.String(name);
            w.String(value);
        }
        w.UInt32((uint)request.Pid);
        w.Bytes(request.Stdin.Span);
        return body;

        static int Str(string s) => 4 + Encoding.UTF8.GetByteCount(s);
    }

    /// <summary>The request in <paramref name="body"/>, or <see langword="null"/> when it is malformed.</summary>
    public static HookRequest? DecodeRequest(ReadOnlyMemory<byte> body)
    {
        var r = new Reader(body);
        if (
            !r.String(out var version)
            || !r.UInt64(out var fingerprint)
            || !r.String(out var hookEvent)
            || !r.String(out var host)
            || !r.String(out var cwd)
            || !r.UInt32(out var count)
            || count > body.Length / 8
        )
        {
            return null;
        }
        var env = new KeyValuePair<string, string>[count];
        for (var i = 0; i < env.Length; i++)
        {
            if (!r.String(out var name) || !r.String(out var value))
            {
                return null;
            }
            env[i] = new(name, value);
        }
        return r.UInt32(out var pid) && r.Bytes(out var stdin) && r.AtEnd
            ? new HookRequest(version, fingerprint, hookEvent, host, cwd, env, (int)pid, stdin)
            : null;
    }

    /// <summary>A reply body: the status byte, then <paramref name="stdout"/> when served.</summary>
    public static byte[] EncodeReply(HookReplyStatus status, ReadOnlySpan<byte> stdout = default)
    {
        var body = new byte[1 + stdout.Length];
        body[0] = (byte)status;
        stdout.CopyTo(body.AsSpan(1));
        return body;
    }

    /// <summary>The status and stdout of a reply body, or <see langword="false"/> when it is malformed.</summary>
    public static bool TryDecodeReply(
        ReadOnlyMemory<byte> body,
        out HookReplyStatus status,
        out ReadOnlyMemory<byte> stdout
    )
    {
        status = default;
        stdout = default;
        if (body.Length == 0 || body.Span[0] > (byte)HookReplyStatus.RefusedFingerprint)
        {
            return false;
        }
        status = (HookReplyStatus)body.Span[0];
        stdout = body[1..];
        return status == HookReplyStatus.Served || stdout.IsEmpty;
    }

    /// <summary><paramref name="body"/> with its little-endian length in front.</summary>
    public static byte[] Frame(ReadOnlySpan<byte> body)
    {
        var frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length);
        body.CopyTo(frame.AsSpan(4));
        return frame;
    }

    /// <summary>
    /// All of <paramref name="input"/> when it holds at most <paramref name="cap"/> bytes, or
    /// <see langword="null"/> once it holds more (the rest is left unread).
    /// </summary>
    public static byte[]? ReadCapped(Stream input, int cap = MaxInput)
    {
        ArgumentNullException.ThrowIfNull(input);
        var buf = new byte[Math.Min(64 << 10, cap + 1)];
        var len = 0;
        while (true)
        {
            if (len == buf.Length)
            {
                if (len > cap)
                {
                    return null;
                }
                Array.Resize(ref buf, (int)Math.Min((long)buf.Length * 2, cap + 1L));
            }
            var n = input.Read(buf, len, buf.Length - len);
            if (n == 0)
            {
                return len > cap ? null : buf[..len];
            }
            len += n;
        }
    }

    private ref struct Writer(Span<byte> buf)
    {
        private readonly Span<byte> _buf = buf;
        private int _at;

        public void UInt32(uint v)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_buf[_at..], v);
            _at += 4;
        }

        public void UInt64(ulong v)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(_buf[_at..], v);
            _at += 8;
        }

        public void String(string s)
        {
            var n = Encoding.UTF8.GetBytes(s, _buf[(_at + 4)..]);
            UInt32((uint)n);
            _at += n;
        }

        public void Bytes(ReadOnlySpan<byte> b)
        {
            UInt32((uint)b.Length);
            b.CopyTo(_buf[_at..]);
            _at += b.Length;
        }
    }

    private struct Reader(ReadOnlyMemory<byte> buf)
    {
        private readonly ReadOnlyMemory<byte> _buf = buf;
        private int _at;

        public readonly bool AtEnd => _at == _buf.Length;

        public bool UInt32(out uint v)
        {
            v = 0;
            if (_buf.Length - _at < 4)
            {
                return false;
            }
            v = BinaryPrimitives.ReadUInt32LittleEndian(_buf.Span[_at..]);
            _at += 4;
            return true;
        }

        public bool UInt64(out ulong v)
        {
            v = 0;
            if (_buf.Length - _at < 8)
            {
                return false;
            }
            v = BinaryPrimitives.ReadUInt64LittleEndian(_buf.Span[_at..]);
            _at += 8;
            return true;
        }

        public bool Bytes(out ReadOnlyMemory<byte> b)
        {
            b = default;
            if (!UInt32(out var n) || n > (uint)(_buf.Length - _at))
            {
                return false;
            }
            b = _buf.Slice(_at, (int)n);
            _at += (int)n;
            return true;
        }

        public bool String(out string s)
        {
            s = "";
            if (!Bytes(out var b))
            {
                return false;
            }
            s = Encoding.UTF8.GetString(b.Span);
            return true;
        }
    }
}
