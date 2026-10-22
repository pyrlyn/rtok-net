using System.Text;
using System.Text.Json;
using Rtok.Agents.Usage;

namespace Rtok.Measure;

/// <summary>
/// One Claude Code transcript, line by line: a JSON object per line, malformed lines counted and
/// skipped. The turn index steps on each <c>user</c> or <c>assistant</c> message; a line that
/// repeats an earlier <c>message.id</c> is another streamed block of the same turn.
/// </summary>
internal sealed class Transcript
{
    /// <summary>A <c>tool_use</c> block: the call as the model sent it.</summary>
    public sealed record ToolUse(string Id, string Name, JsonElement Input, int Turn);

    /// <summary>A <c>tool_result</c> block, its content flattened to text.</summary>
    public sealed record ToolResult(string ToolUseId, string Content, int Turn);

    /// <summary>
    /// A skill body: an <c>isMeta</c> user record whose <c>sourceToolUseID</c> keys it to the
    /// <c>Skill</c> call. Only its size counts; the 22-byte tool_result alone hides it.
    /// </summary>
    public sealed record Injection(string ToolUseId, long Bytes, int Turn);

    /// <summary>One request's counters; <see cref="Ts"/> is unix seconds, 0 when the line has none.</summary>
    public sealed record TurnUsage(
        long Input,
        long CacheCreate,
        long CacheRead,
        long Output,
        int Turn,
        long Ts,
        string? Model
    );

    /// <summary>A <c>thinking</c> or <c>redacted_thinking</c> block's size.</summary>
    public sealed record ThinkingBlock(long Bytes, int Turn);

    /// <summary>
    /// An <c>image</c> block, inside a tool_result (<see cref="ToolUseId"/> set) or a user message
    /// (empty). <see cref="Tokens"/> is 0 when the header is neither PNG nor JPEG.
    /// </summary>
    public sealed record ImageBlock(
        string ToolUseId,
        long Bytes,
        long Tokens,
        bool Sized,
        int Turn
    );

    private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);

    public long Lines { get; private set; }
    public long Malformed { get; private set; }

    /// <summary>Lines that repeat an earlier <c>message.id</c>: one turn, its usage already summed.</summary>
    public long Duplicates { get; private set; }
    public List<ToolUse> ToolUses { get; } = [];
    public List<ToolResult> ToolResults { get; } = [];
    public List<Injection> Injected { get; } = [];
    public List<string> AssistantTexts { get; } = [];
    public List<TurnUsage> Usages { get; } = [];
    public List<ThinkingBlock> Thinking { get; } = [];
    public List<ImageBlock> Images { get; } = [];
    public int Turns { get; private set; }

    /// <summary>The next turn index at each <c>compact_boundary</c>.</summary>
    public List<int> Compactions { get; } = [];

    /// <summary>The first user turn's content, flattened: where a spawn brief lands.</summary>
    public string? FirstUserText { get; private set; }

    /// <summary>A transcript's text, split as <c>str::lines</c> splits it.</summary>
    public static Transcript Parse(string text)
    {
        var t = new Transcript();
        foreach (var line in Json.Lines(text))
        {
            t.Line(line);
        }
        return t;
    }

    /// <summary>
    /// A transcript's bytes. A line that is not valid UTF-8 is one malformed line, never the end
    /// of the file.
    /// </summary>
    public static Transcript Parse(ReadOnlySpan<byte> bytes)
    {
        var t = new Transcript();
        var strict = new UTF8Encoding(false, true);
        while (bytes.Length > 0)
        {
            var end = bytes.IndexOf((byte)'\n');
            var line = end < 0 ? bytes : bytes[..end];
            bytes = end < 0 ? [] : bytes[(end + 1)..];
            if (line.EndsWith("\r"u8))
            {
                line = line[..^1];
            }
            string text;
            try
            {
                text = strict.GetString(line);
            }
            catch (DecoderFallbackException)
            {
                if (!line.Trim(" \t\r\n"u8).IsEmpty)
                {
                    t.Lines++;
                    t.Malformed++;
                }
                continue;
            }
            t.Line(text);
        }
        return t;
    }

    /// <summary>The file at <paramref name="path"/>, or null when it cannot be read.</summary>
    public static Transcript? Read(string path)
    {
        try
        {
            return Parse(File.ReadAllBytes(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A <c>system</c> line with <c>subtype=compact_boundary</c>: one compaction.</summary>
    internal static bool IsCompactBoundary(JsonElement v) =>
        Json.Text(v, "subtype") is "compact_boundary";

    /// <summary>
    /// Content as text: a string as is, an array's strings and <c>text</c> fields joined by
    /// newlines, anything else as its JSON.
    /// </summary>
    internal static string Flatten(JsonElement? v) =>
        v switch
        {
            null or { ValueKind: JsonValueKind.Null } => "",
            { ValueKind: JsonValueKind.String } s => s.GetString() ?? "",
            { ValueKind: JsonValueKind.Array } a => string.Join(
                '\n',
                a.EnumerateArray()
                    .Select(x =>
                        x.ValueKind == JsonValueKind.String ? x.GetString() : Json.Text(x, "text")
                    )
                    .OfType<string>()
            ),
            { } other => other.GetRawText(),
        };

    private void Line(string line)
    {
        line = line.Trim();
        if (line.Length == 0)
        {
            return;
        }
        Lines++;
        if (Json.Parse(line) is { } v)
        {
            Ingest(v);
        }
        else
        {
            Malformed++;
        }
    }

    private void Ingest(JsonElement v)
    {
        var type = Json.Text(v, "type") ?? "";
        var msg = Json.At(v, "message") ?? v;
        var repeat = Json.Text(msg, "id") is { } id && !_seenIds.Add(id);
        var isTurn = type is "user" or "assistant" && !repeat;
        if (isTurn)
        {
            Turns++;
        }
        else if (repeat)
        {
            Duplicates++;
        }
        var turn = Math.Max(Turns - 1, 0);
        if (IsCompactBoundary(v))
        {
            Compactions.Add(Turns);
        }
        if (type is "user" && turn == 0 && FirstUserText is null)
        {
            FirstUserText = Flatten(Json.At(msg, "content"));
        }
        if (
            type is "user"
            && Json.At(v, "isMeta") is { ValueKind: JsonValueKind.True }
            && Json.Text(v, "sourceToolUseID") is { Length: > 0 } source
        )
        {
            Injected.Add(new(source, Utf8(Flatten(Json.At(msg, "content"))), turn));
        }
        if (isTurn && (Json.Object(msg, "usage") ?? Json.Object(v, "usage")) is { } u)
        {
            Usages.Add(
                new(
                    Counter(u, "input_tokens"),
                    Counter(u, "cache_creation_input_tokens"),
                    Counter(u, "cache_read_input_tokens"),
                    Counter(u, "output_tokens"),
                    turn,
                    Json.Timestamp(v),
                    Json.Text(msg, "model")
                )
            );
        }
        switch (Json.At(msg, "content"))
        {
            case { ValueKind: JsonValueKind.String } s
                when type is "assistant" && s.GetString() is { Length: > 0 } text:
                AssistantTexts.Add(text);
                break;
            case { ValueKind: JsonValueKind.Array } blocks:
                foreach (var b in blocks.EnumerateArray())
                {
                    Block(b, type, turn, repeat);
                }
                break;
        }
    }

    private void Block(JsonElement b, string type, int turn, bool repeat)
    {
        switch (Json.Text(b, "type"))
        {
            case "tool_use":
                ToolUses.Add(
                    new(
                        Json.Text(b, "id") ?? "",
                        Json.Text(b, "name") ?? "",
                        Json.At(b, "input") ?? default,
                        turn
                    )
                );
                break;
            case "tool_result":
                var id = Json.Text(b, "tool_use_id") ?? "";
                if (!repeat && Json.At(b, "content") is { ValueKind: JsonValueKind.Array } parts)
                {
                    foreach (var x in parts.EnumerateArray())
                    {
                        Image(x, id, turn);
                    }
                }
                ToolResults.Add(new(id, Flatten(Json.At(b, "content")), turn));
                break;
            case "text" when type is "assistant":
                if (Json.Text(b, "text") is { Length: > 0 } text)
                {
                    AssistantTexts.Add(text);
                }
                break;
            case "image" when type is "user" && !repeat:
                Image(b, "", turn);
                break;
            case "thinking" or "redacted_thinking" when type is "assistant" && !repeat:
                var data = Json.Text(b, "thinking") ?? Json.Text(b, "data") ?? "";
                Thinking.Add(new(Utf8(data), turn));
                break;
        }
    }

    /// <summary>An <c>image</c> block; a base64 source gets its bytes and pixel size.</summary>
    private void Image(JsonElement x, string toolUseId, int turn)
    {
        if (Json.Text(x, "type") is not "image")
        {
            return;
        }
        var data = Json.Text(x, "source", "data") ?? "";
        var dims = ImageTokens.Dims(data);
        Images.Add(
            new(
                toolUseId,
                ImageTokens.DecodedLength(data),
                dims is var (w, h) ? ImageTokens.Tokens(w, h) : 0,
                dims is not null,
                turn
            )
        );
    }

    /// <summary>A counter as Claude Code's parser keeps it: unsigned, saturating at <see cref="uint.MaxValue"/>.</summary>
    private static long Counter(JsonElement usage, string name) =>
        Json.At(usage, name) is { ValueKind: JsonValueKind.Number } n && n.TryGetUInt64(out var u)
            ? (long)Math.Min(u, uint.MaxValue)
            : 0;

    internal static long Utf8(string s) => Encoding.UTF8.GetByteCount(s);
}
