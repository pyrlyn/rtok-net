namespace Rtok.Config;

/// <summary>rtok agents usage</summary>
/// <remarks>Read from [agents.usage].</remarks>
public sealed class AgentsUsageSection
{
    /// <summary>logs = the agents' own session files (Claude Code, Codex, OpenCode, Kilo, Copilot CLI, Gemini CLI); rtok = what passed through rtok; both</summary>
    public string Source { get; set; } = "logs";

    /// <summary>[] = every host; else host ids, e.g. ["claude", "codex"]</summary>
    public IList<string> Hosts { get; set; } = [];

    /// <summary>"" = all time; a date (2026-09-01, whole days in tz) or a duration (30d)</summary>
    public string Since { get; set; } = "";

    /// <summary>"" = through today; a date, inclusive</summary>
    public string Until { get; set; } = "";

    /// <summary>monthly | daily: the bottom table</summary>
    public string Period { get; set; } = "monthly";

    /// <summary>agent | model: what the middle table groups by</summary>
    public string By { get; set; } = "agent";

    /// <summary>IANA zone for day and month boundaries; "" = the system zone</summary>
    public string Tz { get; set; } = "";

    /// <summary>The [agents.usage.dirs] table.</summary>
    public UsageDirsSection Dirs { get; set; } = new();
}
