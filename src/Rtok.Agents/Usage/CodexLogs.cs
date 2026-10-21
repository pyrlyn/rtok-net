namespace Rtok.Agents.Usage;

/// <summary>
/// Codex CLI rollouts (<c>~/.codex/sessions/**/*.jsonl</c>): one <c>event_msg</c> line of
/// <c>payload.type == "token_count"</c> per API request. Its <c>last_token_usage</c> is that
/// request's counters; <c>input_tokens</c> includes <c>cached_input_tokens</c> and
/// <c>output_tokens</c> includes reasoning. The model is the latest one a line named before it.
/// </summary>
internal static class CodexLogs
{
    public static HostLog Read(string dir, long since)
    {
        var slices = new List<UsageSlice>();
        string? unreadable = null;
        foreach (var path in LogFiles.Find(dir, LogSlice.Cutoff(since), "jsonl"))
        {
            if (LogText.Read(path) is { } text && Rollout(path, text, since) is { } rows)
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

    /// <summary>The requests of one rollout; null when it has text but no line of it was JSON.</summary>
    internal static List<UsageSlice>? Rollout(string path, string text, long since)
    {
        var rows = new List<UsageSlice>();
        var json = 0;
        var session = Path.GetFileNameWithoutExtension(path);
        string? model = null;
        foreach (var line in Json.Lines(text))
        {
            if (Json.Parse(line) is not { } v)
            {
                continue;
            }
            json++;
            model = Json.Text(v, "payload", "model") ?? model;
            if (Json.Text(v, "type") is "session_meta" && Json.Text(v, "payload", "id") is { } id)
            {
                session = id;
            }
            if (
                Json.At(v, "payload", "info", "last_token_usage") is not { } u
                || Json.Text(v, "payload", "type") is not "token_count"
            )
            {
                continue;
            }
            var cached = Json.Count(u, "cached_input_tokens");
            var slice = LogSlice.Of(
                "codex",
                "openai",
                session,
                model,
                Json.Timestamp(v),
                (
                    Math.Max(Json.Count(u, "input_tokens") - cached, 0),
                    Json.Count(u, "cache_write_input_tokens"),
                    cached,
                    Json.Count(u, "output_tokens")
                ),
                since
            );
            if (slice is not null)
            {
                rows.Add(slice);
            }
        }
        return json > 0 || string.IsNullOrWhiteSpace(text) ? rows : null;
    }
}
