using Rtok.Agents.Usage;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any text as one host's log file, the first byte picking the host: no reader throws, and every
/// request it yields has non-negative legs and total.
/// </summary>
internal static class UsageLogs
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        IEnumerable<UsageSlice> rows = (pick % 8) switch
        {
            0 => ClaudeLogs.Transcript("/p/s.jsonl", text, 0) ?? [],
            1 => CodexLogs.Rollout("/s/rollout.jsonl", text, 0) ?? [],
            2 => CopilotLogs.Session("/s/events.jsonl", text, 0),
            3 => GeminiLogs.Session("/chats/session-1.jsonl", text, 0) ?? [],
            4 => GeminiLogs.Session("/chats/session-1.json", text, 0) ?? [],
            5 => PiLogs.Session("/s/1.jsonl", text, 0, []) ?? [],
            6 => KimiLogs.Wire("/s/agents/wire.jsonl", text, 0),
            _ => HostDatabases.Request("opencode", "s", 0, text, 0) is { } one ? [one] : [],
        };
        foreach (var row in rows)
        {
            FuzzInput.Check(
                row
                    is {
                        Input: >= 0,
                        CacheCreate: >= 0,
                        CacheRead: >= 0,
                        Output: >= 0,
                        Tokens: >= 0
                    },
                $"negative counter in {row}"
            );
        }
    }
}
