namespace Rtok.Agents.Usage;

/// <summary>
/// Kimi Code (<c>$KIMI_CODE_HOME/sessions/&lt;work dir key&gt;/&lt;session&gt;/agents/&lt;agent&gt;/wire.jsonl</c>):
/// every agent of a session, the main one and its sub-agents, appends to its own wire log. Each
/// LLM request ends in a <c>usage.record</c> line with its own <c>usage</c>, <c>model</c> and a
/// millisecond <c>time</c>. Every record is one request, so all are summed. The legacy
/// <c>~/.kimi</c> of the archived kimi-cli is a different format and is not read.
/// </summary>
internal static class KimiLogs
{
    private const string Record = "usage.record";

    public static HostLog Read(IEnumerable<string> dirs, long since)
    {
        var slices = new List<UsageSlice>();
        string? unreadable = null;
        var files = LogFiles
            .Find(dirs, LogSlice.Cutoff(since), "jsonl")
            .Where(p => Path.GetFileName(p) is "wire.jsonl");
        foreach (var path in files)
        {
            if (LogText.Read(path) is { } text)
            {
                slices.AddRange(Wire(path, text, since));
            }
            else
            {
                unreadable ??= path;
            }
        }
        return new HostLog(slices, unreadable);
    }

    /// <summary>The session a wire log belongs to: the folder above <c>agents/</c>.</summary>
    internal static string SessionOf(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var agents = Array.LastIndexOf(parts, "agents");
        return agents > 0 ? parts[agents - 1] : "";
    }

    internal static IEnumerable<UsageSlice> Wire(string path, string text, long since)
    {
        var id = SessionOf(path);
        // Most of a log is context and tool events: only the usage lines are parsed.
        foreach (
            var line in Json.Lines(text)
                .Where(l => l.Contains("\"usage.record\"", StringComparison.Ordinal))
        )
        {
            if (
                Json.Parse(line) is not { } v
                || Json.Text(v, "type") is not Record
                || Json.At(v, "usage") is not { } u
            )
            {
                continue;
            }
            var slice = LogSlice.Of(
                "kimi",
                "moonshot",
                id,
                Json.Text(v, "model"),
                (Json.Integer(v, "time") ?? 0) / 1000,
                (
                    Json.Count(u, "inputOther"),
                    Json.Count(u, "inputCacheCreation"),
                    Json.Count(u, "inputCacheRead"),
                    Json.Count(u, "output")
                ),
                since
            );
            if (slice is not null)
            {
                yield return slice;
            }
        }
    }
}
