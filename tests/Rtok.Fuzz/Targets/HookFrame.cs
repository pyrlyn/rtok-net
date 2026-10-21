using System.Buffers.Binary;
using Rtok.Hook.Contracts;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any bytes through <see cref="HookWire"/>: a request body decodes or is refused, and a decoded
/// one encodes to a body that decodes the same; a reply decodes or is refused, and a decoded one
/// encodes back to its bytes; <see cref="HookWire.ReadCapped"/> (cap from the first byte) returns
/// the input up to its cap, null past it, and never reads more than one byte past the cap.
/// </summary>
internal static class HookFrame
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var bytes = data.ToArray();
        if (HookWire.DecodeRequest(bytes) is { } request)
        {
            var again = HookWire.DecodeRequest(HookWire.EncodeRequest(request));
            FuzzInput.Check(
                again is not null && Same(request, again),
                "a decoded request does not encode back to itself"
            );
        }
        if (HookWire.TryDecodeReply(bytes, out var status, out var stdout))
        {
            FuzzInput.Check(
                HookWire.EncodeReply(status, stdout.Span).AsSpan().SequenceEqual(data),
                "a decoded reply does not encode back to its bytes"
            );
        }
        var frame = HookWire.Frame(data);
        FuzzInput.Check(
            BinaryPrimitives.ReadInt32LittleEndian(frame) == data.Length
                && frame.AsSpan(4).SequenceEqual(data),
            "a frame does not carry its body"
        );
        var cap = data.IsEmpty ? 0 : data[0];
        using var input = new MemoryStream(bytes);
        var read = HookWire.ReadCapped(input, cap);
        FuzzInput.Check(
            read is null ? data.Length > cap : read.AsSpan().SequenceEqual(data),
            "ReadCapped does not return the input up to its cap"
        );
        FuzzInput.Check(input.Position <= cap + 1, "ReadCapped read past its cap");
    }

    private static bool Same(HookRequest a, HookRequest b) =>
        string.Equals(a.Version, b.Version, StringComparison.Ordinal)
        && a.Fingerprint == b.Fingerprint
        && string.Equals(a.Event, b.Event, StringComparison.Ordinal)
        && string.Equals(a.Host, b.Host, StringComparison.Ordinal)
        && string.Equals(a.Cwd, b.Cwd, StringComparison.Ordinal)
        && a.HostEnv.SequenceEqual(b.HostEnv)
        && a.Pid == b.Pid
        && a.Stdin.Span.SequenceEqual(b.Stdin.Span);
}
