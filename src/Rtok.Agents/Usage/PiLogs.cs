using System.Text.Json;

namespace Rtok.Agents.Usage;

/// <summary>
/// pi (<c>~/.pi/agent/sessions/&lt;cwd&gt;/*.jsonl</c>): one entry per line. The entries that spent
/// tokens carry a <c>usage</c> object (<c>input</c>, <c>output</c>, <c>cacheRead</c>,
/// <c>cacheWrite</c>, each one request's own count): an assistant <c>message</c> under
/// <c>message.usage</c>, and a <c>compaction</c>, <c>branch_summary</c> or <c>usage</c> entry
/// directly. Reasoning is already inside <c>output</c>. A forked session copies its parent's
/// entries, so an entry counts once however many files hold it, keyed by its id and timestamp.
/// </summary>
internal static class PiLogs
{
    public static HostLog Read(IEnumerable<string> dirs, long since)
    {
        var seen = new HashSet<(string, string)>();
        var slices = new List<UsageSlice>();
        string? unreadable = null;
        foreach (var path in LogFiles.Find(dirs, LogSlice.Cutoff(since), "jsonl"))
        {
            if (LogText.Read(path) is { } text && Session(path, text, since, seen) is { } rows)
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

    /// <summary>The requests of one session file; null when it has text but no line of it was JSON.</summary>
    internal static List<UsageSlice>? Session(
        string path,
        string text,
        long since,
        HashSet<(string, string)> seen
    )
    {
        var id = Path.GetFileNameWithoutExtension(path);
        var rows = new List<UsageSlice>();
        var parsed = 0;
        foreach (var line in Json.Lines(text).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            if (Json.Parse(line) is not { } v)
            {
                continue;
            }
            parsed++;
            if (Json.Text(v, "type") is "session")
            {
                id = Json.Text(v, "id") ?? id;
                continue;
            }
            if (Entry(id, v, since, seen) is { } slice)
            {
                rows.Add(slice);
            }
        }
        return parsed > 0 || string.IsNullOrWhiteSpace(text) ? rows : null;
    }

    private static UsageSlice? Entry(
        string session,
        JsonElement v,
        long since,
        HashSet<(string, string)> seen
    )
    {
        var usage = Json.At(v, "usage") ?? Json.At(v, "message", "usage");
        if (
            usage is not { ValueKind: JsonValueKind.Object } u
            || Json.Text(v, "timestamp") is not { } stamp
        )
        {
            return null;
        }
        if (Json.Text(v, "id") is { } entry && !seen.Add((entry, stamp)))
        {
            return null;
        }
        string? Field(string name) =>
            Json.At(v, name) is { } own
                ? own.ValueKind == JsonValueKind.String
                    ? own.GetString()
                    : null
                : Json.Text(v, "message", name);
        return LogSlice.Of(
            "pi",
            Field("provider") ?? "pi",
            session,
            Field("model"),
            Json.Timestamp(v),
            (
                Json.Count(u, "input"),
                Json.Count(u, "cacheWrite"),
                Json.Count(u, "cacheRead"),
                Json.Count(u, "output")
            ),
            since
        );
    }
}
