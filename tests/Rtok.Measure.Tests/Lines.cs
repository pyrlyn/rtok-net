using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Measure.Tests;

/// <summary>Transcript lines for the tests, and the collect call every one of them ends with.</summary>
internal static class Lines
{
    public static readonly Replay Defaults = Replay.From(new RtokConfig());

    /// <summary>An assistant line with one tool_use block.</summary>
    public static JsonObject Use(string id, string name, JsonObject input, string? msgId = null) =>
        new()
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject
            {
                ["id"] = msgId ?? "m" + id,
                ["content"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = id,
                        ["name"] = name,
                        ["input"] = input,
                    }
                ),
            },
        };

    /// <summary>A user line with one tool_result block.</summary>
    public static JsonObject Result(string id, JsonNode content) =>
        new()
        {
            ["type"] = "user",
            ["message"] = new JsonObject
            {
                ["content"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = id,
                        ["content"] = content,
                    }
                ),
            },
        };

    public static JsonObject Obj(params (string Key, JsonNode? Value)[] fields)
    {
        var o = new JsonObject();
        foreach (var (k, v) in fields)
        {
            o[k] = v;
        }
        return o;
    }

    public static string Write(TempDir dir, string name, IEnumerable<JsonNode> lines) =>
        dir.Write(name, string.Concat(lines.Select(l => l.ToJsonString() + "\n")));

    /// <summary>Writes JSON lines given as text, one per line.</summary>
    public static string Raw(TempDir dir, string name, params string[] lines) =>
        dir.Write(name, string.Concat(lines.Select(l => l + "\n")));

    public static StatsReport Collect(
        TempDir dir,
        Replay? replay = null,
        StatsSignals? signals = null,
        string plugin = ""
    ) =>
        StatsCollector.Collect(
            dir.Path,
            DateTime.UtcNow.AddDays(-60),
            plugin,
            replay ?? Defaults,
            signals ?? StatsSignals.None
        );

    /// <summary>A base64 PNG of only its signature and IHDR, the size given.</summary>
    public static string Png(int w, int h)
    {
        var b = new byte[24];
        ((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).CopyTo(b);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8), 13);
        "IHDR"u8.CopyTo(b.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16), (uint)w);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20), (uint)h);
        return Convert.ToBase64String(b);
    }

    /// <summary>A base64 JPEG of SOI and one SOF0 segment, the size given.</summary>
    public static string Jpeg(int w, int h)
    {
        byte[] b = [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0, 0, 0, 0, 0x03, 0, 0, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(7), (ushort)h);
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(9), (ushort)w);
        return Convert.ToBase64String(b);
    }
}
