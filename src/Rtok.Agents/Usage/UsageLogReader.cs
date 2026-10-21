using Rtok.Config;

namespace Rtok.Agents.Usage;

/// <summary>
/// Reads every host's session files: Claude Code and Codex from the <c>[stats]</c> folders, the
/// others from <c>[agents.usage.dirs]</c>. A host whose files exist but gave no request is named
/// once in <see cref="UsageLogs.Skipped"/> and counted nowhere.
/// </summary>
public static class UsageLogReader
{
    /// <summary>Why a host's files are named without being counted: they exist but no request came out.</summary>
    internal const string Unknown = "unknown format";

    /// <summary>
    /// The hosts whose token fields are not documented: named when they left files, never guessed
    /// at (<c>research.md</c>). xAI says to run <c>grok usage</c> instead of reading the files, and
    /// rtok does not run another host's program.
    /// </summary>
    internal static readonly (string Host, string Reason)[] Unsupported =
    [
        ("droid", "unsupported: the session settings fields are not documented"),
        ("grok", "unsupported: xAI documents `grok usage`, not the session files"),
        ("zcode", "unsupported: the session records are not documented"),
        ("antigravity", "unsupported: the local session data is not documented"),
    ];

    /// <summary>Every host's requests stamped at or after <paramref name="since"/> (unix seconds).</summary>
    public static UsageLogs Read(StatsSection stats, UsageDirsSection dirs, long since)
    {
        var slices = new List<UsageSlice>();
        var skipped = new List<UsageSkip>();
        void Add(string host, string reason, HostLog log)
        {
            slices.AddRange(log.Slices);
            if (log.Unreadable is { } path)
            {
                skipped.Add(new UsageSkip(host, reason, path));
            }
        }
        Add("claude", Unknown, ClaudeLogs.Read(stats.TranscriptsDir, since));
        Add("codex", Unknown, CodexLogs.Read(stats.CodexDir, since));
        Add("opencode", Unknown, HostDatabases.Read("opencode", dirs.Opencode, since));
        Add("kilo", Unknown, HostDatabases.Read("kilo", dirs.Kilo, since));
        Add("copilot", Unknown, CopilotLogs.Read(dirs.Copilot, since));
        Add("gemini", Unknown, GeminiLogs.Read(dirs.Gemini, since));
        Add("pi", Unknown, PiLogs.Read(dirs.Pi, since));
        Add("kimi", Unknown, KimiLogs.Read(dirs.Kimi, since));
        IList<string>[] undocumented = [dirs.Droid, dirs.Grok, dirs.Zcode, dirs.Antigravity];
        for (var i = 0; i < Unsupported.Length; i++)
        {
            Add(
                Unsupported[i].Host,
                Unsupported[i].Reason,
                new HostLog([], LogFiles.FirstNonEmpty(undocumented[i]))
            );
        }
        return new UsageLogs(slices, skipped);
    }
}
