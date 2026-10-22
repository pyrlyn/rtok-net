using Rtok.Agents.Usage;

namespace Rtok.Measure;

/// <summary>
/// Every host's session logs as <see cref="CallRecord"/>s. Claude Code's transcripts are read
/// here, with each sub-agent billed to its parent; Codex and OpenCode come through the readers
/// <c>rtok agents usage</c> uses, and Cursor through <see cref="CursorLogs"/>.
/// </summary>
public static class CallRecords
{
    /// <summary>
    /// <c>projects/**/*.jsonl</c> modified at or after <paramref name="cutoff"/>: one record per
    /// request with usage, each sub-agent's (<c>&lt;session&gt;/subagents/agent-*.jsonl</c>)
    /// under its parent's session and its <c>meta.json</c> type.
    /// </summary>
    public static IReadOnlyList<CallRecord> Claude(string dir, DateTime cutoff)
    {
        var records = new List<CallRecord>();
        foreach (var path in LogFiles.Find(dir, cutoff, "jsonl"))
        {
            if (Transcript.Read(path) is not { } t)
            {
                continue;
            }
            var session = ClaudeLogs.SessionOf(path);
            string? agent = Subagents.IsSubagent(path)
                ? Subagents.Meta(path, new Dictionary<string, int>(StringComparer.Ordinal)).Type
                : null;
            var tools = t.ToolUses.ToLookup(u => u.Turn, u => u.Name);
            records.AddRange(
                t.Usages.Select(u => new CallRecord(
                    "claude",
                    session,
                    agent,
                    u.Model,
                    u.Ts,
                    u.Input,
                    u.CacheCreate,
                    u.CacheRead,
                    u.Output,
                    [.. tools[u.Turn]]
                ))
            );
        }
        return records;
    }

    /// <summary>Codex CLI rollouts (<c>sessions/**/rollout-*.jsonl</c>) stamped at or after <paramref name="since"/> (unix seconds).</summary>
    public static IReadOnlyList<CallRecord> Codex(string dir, long since) =>
        Of(CodexLogs.Read(dir, since).Slices);

    /// <summary>OpenCode's <c>opencode*.db</c> in <paramref name="dirs"/>: each assistant message.</summary>
    public static IReadOnlyList<CallRecord> OpenCode(IEnumerable<string> dirs, long since) =>
        Of(HostDatabases.Read("opencode", dirs, since).Slices);

    private static List<CallRecord> Of(IEnumerable<UsageSlice> slices) =>
        [
            .. slices.Select(s => new CallRecord(
                s.Host ?? "",
                s.Session,
                null,
                s.Model,
                s.Ts,
                s.Input,
                s.CacheCreate,
                s.CacheRead,
                s.Output,
                []
            )),
        ];
}
