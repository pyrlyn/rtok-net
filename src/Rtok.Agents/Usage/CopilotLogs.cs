using System.Text.Json;

namespace Rtok.Agents.Usage;

/// <summary>
/// GitHub Copilot CLI (<c>session-state/&lt;session&gt;/events.jsonl</c>). The per-request
/// <c>assistant.usage</c> event is never persisted, so the only token counts on disk are the
/// <c>modelMetrics</c> of the <c>session.shutdown</c> event: one total per model for the whole
/// session. A session lands on the day it ended, and one that never shut down cleanly counts
/// nowhere rather than being estimated.
/// </summary>
internal static class CopilotLogs
{
    private const string Shutdown = "session.shutdown";

    public static HostLog Read(IEnumerable<string> dirs, long since)
    {
        var slices = new List<UsageSlice>();
        string? unreadable = null;
        var files = LogFiles
            .Find(dirs, LogSlice.Cutoff(since), "jsonl")
            .Where(p => Path.GetFileName(p) is "events.jsonl");
        foreach (var path in files)
        {
            if (LogText.Read(path) is { } text)
            {
                slices.AddRange(Session(path, text, since));
            }
            else
            {
                unreadable ??= path;
            }
        }
        return new HostLog(slices, unreadable);
    }

    internal static IEnumerable<UsageSlice> Session(string path, string text, long since)
    {
        var id = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
        // A resumed session appends another shutdown whose totals are session-wide, so only
        // the last one counts: summing would count the earlier runs again on every resume.
        (long Ts, JsonElement Models)? last = null;
        foreach (
            var line in Json.Lines(text)
                .Where(l => l.Contains("\"session.shutdown\"", StringComparison.Ordinal))
        )
        {
            if (
                Json.Parse(line) is { } v
                && Json.Text(v, "type") is Shutdown
                && Json.Object(v, "data", "modelMetrics") is { } models
            )
            {
                last = (Json.Timestamp(v), models);
            }
        }
        if (last is not { } end)
        {
            yield break;
        }
        foreach (var model in end.Models.EnumerateObject())
        {
            if (
                LogSlice.Of(
                    "copilot",
                    "github",
                    id,
                    model.Name,
                    end.Ts,
                    Legs(model.Value),
                    since
                ) is
                { } slice
            )
            {
                yield return slice;
            }
        }
    }

    /// <summary>
    /// One model's legs. <c>tokenDetails</c> is the whole spend: <c>usage</c> plus the
    /// context-compaction requests <c>usage</c> leaves out, and its <c>input</c> is uncached already.
    /// Without both its <c>input</c> and <c>output</c> it falls back to <c>usage</c>, whose
    /// <c>inputTokens</c> includes the cache legs.
    /// </summary>
    private static (long, long, long, long) Legs(JsonElement metric)
    {
        long? Detail(string leg) =>
            Json.Integer(metric, "tokenDetails", leg, "tokenCount") is not null
                ? Json.Count(metric, "tokenDetails", leg, "tokenCount")
                : null;
        if (Detail("input") is { } input && Detail("output") is { } output)
        {
            return (input, Detail("cache_write") ?? 0, Detail("cache_read") ?? 0, output);
        }
        var read = Json.Count(metric, "usage", "cacheReadTokens");
        var write = Json.Count(metric, "usage", "cacheWriteTokens");
        return (
            Math.Max(Json.Count(metric, "usage", "inputTokens") - read - write, 0),
            write,
            read,
            Json.Count(metric, "usage", "outputTokens")
        );
    }
}
