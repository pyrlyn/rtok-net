using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rtok.Hooks.Hosts;

/// <summary>Compact JSON as the hosts read it, written by hand so it stays AOT-clean. Only
/// quotes, backslashes and control characters are escaped, as rtok wrote them.</summary>
internal static class HookJson
{
    private static readonly JsonWriterOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Bytes(JsonNode node) => Write(w => node.WriteTo(w));

    public static byte[] Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Options))
        {
            write(writer);
        }
        return buffer.WrittenSpan.ToArray();
    }
}
