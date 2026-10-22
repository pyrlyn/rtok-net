using Rtok.Agents.Usage;
using Rtok.Measure;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any text as one session log for <c>rtok stats</c>, the first byte picking the reader: Claude
/// Code's transcript (with the dictionary fold over it), a Codex rollout, an OpenCode message or
/// a Cursor bubble. None throws, and every token count is non-negative.
/// </summary>
internal static class TranscriptJsonl
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        switch (pick % 4)
        {
            case 0:
                var t = Transcript.Parse(text);
                new DictionaryRow().Fold(t);
                foreach (var u in t.Usages)
                {
                    Legs(u.Input, u.CacheCreate, u.CacheRead, u.Output, u.ToString());
                }
                break;
            case 1:
                foreach (var s in CodexLogs.Rollout("/s/rollout.jsonl", text, 0) ?? [])
                {
                    Legs(s.Input, s.CacheCreate, s.CacheRead, s.Output, s.ToString());
                }
                break;
            case 2:
                if (HostDatabases.Request("opencode", "s", 0, text, 0) is { } o)
                {
                    Legs(o.Input, o.CacheCreate, o.CacheRead, o.Output, o.ToString());
                }
                break;
            default:
                if (CursorLogs.Bubble("c", text) is { } c)
                {
                    Legs(c.Input, c.CacheCreate, c.CacheRead, c.Output, c.ToString());
                }
                break;
        }
    }

    private static void Legs(long input, long create, long read, long output, string row) =>
        FuzzInput.Check(
            input >= 0 && create >= 0 && read >= 0 && output >= 0,
            $"negative counter in {row}"
        );
}
