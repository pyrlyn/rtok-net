using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Rtok.Agents.Usage;
using Rtok.Store;

namespace Rtok.Measure;

/// <summary>
/// <c>rtok stats --cache</c>: prompt-cache health per session from the proxy's <c>usage</c> rows.
/// A bust is a turn whose cache reads fell below the previous turn's while it wrote more than
/// <see cref="BustCreateTokens"/> (OpenAI: on the drop alone). Its cause comes from the recorded
/// request bodies: a changed <c>tools</c> array, a changed <c>system</c> prompt, else
/// <c>unknown</c> (no body, or the change was inside <c>messages</c>).
/// </summary>
public static class CacheHealth
{
    public const long BustCreateTokens = 20_000;

    /// <summary>One API request as the cache saw it; <see cref="Bust"/> names the cause of a bust.</summary>
    public sealed record Turn(
        long? CallId,
        long Input,
        long CacheCreate,
        long CacheRead,
        string? Bust
    );

    public sealed record Session(string Id, IReadOnlyList<Turn> Turns)
    {
        public int Busts => Turns.Count(t => t.Bust is not null);
        public long CacheRead => Turns.Sum(t => t.CacheRead);
        public long CacheCreate => Turns.Sum(t => t.CacheCreate);
    }

    /// <summary>Every session with usage rows at or after <paramref name="since"/> (Unix
    /// seconds), oldest first.</summary>
    public static IReadOnlyList<Session> Report(RtokStore store, long since = 0)
    {
        ArgumentNullException.ThrowIfNull(store);
        return
        [
            .. store
                .UsageSessions(since)
                .Select(s => new Session(
                    s,
                    Analyse(store.UsageTurns(s, since), store.CallRequest)
                )),
        ];
    }

    /// <summary>
    /// Turns with busts attributed. <paramref name="body"/> is asked only for the two requests
    /// around a bust.
    /// </summary>
    internal static IReadOnlyList<Turn> Analyse(
        IReadOnlyList<UsageTurn> rows,
        Func<long, byte[]?> body
    )
    {
        (string? Tools, string? System)? Load(long? id) =>
            id is { } i && body(i) is { } b ? Shape(b) : null;
        var turns = new List<Turn>(rows.Count);
        UsageTurn? prev = null;
        foreach (var r in rows)
        {
            var busted =
                prev is not null
                && r.CacheRead < prev.CacheRead
                && (
                    r.Api is "openai_chat" or "openai_responses" || r.CacheCreate > BustCreateTokens
                );
            var cause = busted ? Cause(Load(prev!.CallId), Load(r.CallId)) : null;
            turns.Add(new Turn(r.CallId, r.Input, r.CacheCreate, r.CacheRead, cause));
            prev = r;
        }
        return turns;
    }

    internal static string Table(IReadOnlyList<Session> report)
    {
        List<IReadOnlyList<string>> rows =
        [
            ["session", "turns", "cache_read", "cache_create", "busts"],
        ];
        rows.AddRange(
            report.Select(h =>
                (IReadOnlyList<string>)
                    [h.Id, Num(h.Turns.Count), Num(h.CacheRead), Num(h.CacheCreate), Num(h.Busts)]
            )
        );
        var s = TextTable.Render([false, true, true, true, true], rows, [40, 6, 12, 12, 6]);
        foreach (var h in report)
        {
            foreach (
                var (t, i) in h.Turns.Select((t, i) => (t, i)).Where(x => x.t.Bust is not null)
            )
            {
                s += string.Create(
                    CultureInfo.InvariantCulture,
                    $"  bust turn {i + 1} cause={t.Bust} cache_create={t.CacheCreate} cache_read={t.CacheRead}\n"
                );
            }
        }
        return report.Count == 0 ? s + "no usage rows (run `rtok proxy` first)\n" : s;
    }

    internal static string Json(IReadOnlyList<Session> report)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartArray();
            foreach (var h in report)
            {
                w.WriteStartObject();
                w.WriteString("session", h.Id);
                w.WriteStartArray("turns");
                foreach (var t in h.Turns)
                {
                    w.WriteStartObject();
                    if (t.CallId is { } id)
                    {
                        w.WriteNumber("call_id", id);
                    }
                    else
                    {
                        w.WriteNull("call_id");
                    }
                    w.WriteNumber("input", t.Input);
                    w.WriteNumber("cache_create", t.CacheCreate);
                    w.WriteNumber("cache_read", t.CacheRead);
                    w.WriteString("bust", t.Bust);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteNumber("busts", h.Busts);
                w.WriteNumber("cache_read", h.CacheRead);
                w.WriteNumber("cache_create", h.CacheCreate);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string Cause(
        (string? Tools, string? System)? prev,
        (string? Tools, string? System)? cur
    ) =>
        (prev, cur) switch
        {
            ({ } p, { } c) when !string.Equals(p.Tools, c.Tools, StringComparison.Ordinal) =>
                "tools",
            ({ } p, { } c) when !string.Equals(p.System, c.System, StringComparison.Ordinal) =>
                "system",
            _ => "unknown",
        };

    /// <summary>Fingerprints of the request parts whose change busts the cache; none for a body that is not JSON.</summary>
    private static (string? Tools, string? System) Shape(byte[] body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return (Fingerprint(doc.RootElement, "tools"), Fingerprint(doc.RootElement, "system"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>SHA-256 of the part re-written compact, so whitespace alone is no change.</summary>
    private static string? Fingerprint(JsonElement root, string key)
    {
        if (root.ValueKind is not JsonValueKind.Object || !root.TryGetProperty(key, out var part))
        {
            return null;
        }
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            part.WriteTo(w);
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private static string Num(long n) => n.ToString(CultureInfo.InvariantCulture);
}
