using System.Text.Json;

namespace Rtok.Agents.Usage;

/// <summary>
/// Claude Code transcripts (<c>~/.claude/projects/&lt;project&gt;/&lt;session&gt;.jsonl</c>): one
/// request per <c>message.id</c>. A streamed message is one line per content block, each repeating
/// the same <c>usage</c>, so only its first line counts. A sub-agent transcript
/// (<c>&lt;session&gt;/subagents/*.jsonl</c>) counts toward its parent's session.
/// </summary>
internal static class ClaudeLogs
{
    public static HostLog Read(string dir, long since)
    {
        var slices = new List<UsageSlice>();
        string? unreadable = null;
        foreach (var path in LogFiles.Find(dir, LogSlice.Cutoff(since), "jsonl"))
        {
            if (LogText.Read(path) is { } text && Transcript(path, text, since) is { } rows)
            {
                slices.AddRange(rows);
            }
            else
            {
                unreadable ??= path;
            }
        }
        return new HostLog(slices, unreadable);
    }

    /// <summary>The requests of one transcript; null when it has lines but none of them was JSON.</summary>
    internal static List<UsageSlice>? Transcript(string path, string text, long since)
    {
        var rows = new List<UsageSlice>();
        var (lines, malformed) = (0, 0);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var session = SessionOf(path);
        foreach (var raw in Json.Lines(text))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            lines++;
            if (Json.Parse(line) is not { } v)
            {
                malformed++;
                continue;
            }
            if (Request(v, session, seen, since) is { } slice)
            {
                rows.Add(slice);
            }
        }
        return lines > 0 && malformed == lines ? null : rows;
    }

    /// <summary>The session a transcript belongs to: its own stem, or its parent's for a sub-agent.</summary>
    internal static string SessionOf(string path)
    {
        var parent = Path.GetDirectoryName(path);
        var owner = Path.GetFileName(parent) is "subagents" ? Path.GetDirectoryName(parent) : path;
        return owner is null ? "" : Path.GetFileNameWithoutExtension(owner);
    }

    private static UsageSlice? Request(
        JsonElement v,
        string session,
        HashSet<string> seen,
        long since
    )
    {
        var type = Json.Text(v, "type");
        var message = Json.At(v, "message") ?? v;
        var repeat = Json.Text(message, "id") is { } id && !seen.Add(id);
        if (repeat || type is not ("user" or "assistant"))
        {
            return null;
        }
        if ((Json.Object(message, "usage") ?? Json.Object(v, "usage")) is not { } u)
        {
            return null;
        }
        return LogSlice.Of(
            "claude",
            "anthropic",
            session,
            Json.Text(message, "model"),
            Json.Timestamp(v),
            (
                Counter(u, "input_tokens"),
                Counter(u, "cache_creation_input_tokens"),
                Counter(u, "cache_read_input_tokens"),
                Counter(u, "output_tokens")
            ),
            since
        );
    }

    /// <summary>A counter as Claude Code's parser keeps it: unsigned, saturating at <see cref="uint.MaxValue"/>.</summary>
    private static long Counter(JsonElement usage, string name) =>
        Json.At(usage, name) is { ValueKind: JsonValueKind.Number } n && n.TryGetUInt64(out var u)
            ? (long)Math.Min(u, uint.MaxValue)
            : 0;
}
