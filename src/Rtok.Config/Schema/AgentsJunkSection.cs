namespace Rtok.Config;

/// <summary>rtok agents junk list|clear: age floors, protected paths, your own junk paths</summary>
/// <remarks>Read from [agents.junk].</remarks>
public sealed class AgentsJunkSection
{
    /// <summary>`logs` entries modified within this many days stay (0-3650)</summary>
    public uint KeepLogsDays { get; set; } = 30;

    /// <summary>`temp` entries touched within this many hours stay (0-87600)</summary>
    public uint TempMinAgeHours { get; set; } = 24;

    /// <summary>`--kind sessions` only: sessions not touched for more than this many days are junk (0-3650); `--session-days N` for one run</summary>
    public uint StaleSessionDays { get; set; } = 30;

    /// <summary>a merged, clean worktree `rtok worktree gc` would remove is junk (`stale-worktrees`, review) once idle this many days (0-3650)</summary>
    public uint StaleWorktreeDays { get; set; } = 14;

    /// <summary>a crash dump in an `extra` crash-dumps folder older than this is safe to clear, a younger one is review (0-3650)</summary>
    public uint CrashDumpMinAgeDays { get; set; } = 7;

    /// <summary>globs never touched, nor any folder holding a match, e.g. ["~/.claude/debug/keep-*"]</summary>
    public IList<string> Exclude { get; set; } = [];

    /// <summary>paths you vouch for as junk, e.g. [{ host = "cursor", kind = "cache", path = "~/Library/Application Support/Cursor/CachedData" }]; kind = cache | temp | logs | crash-dumps; host = a host id or rtok</summary>
    public IList<JunkExtra> Extra { get; set; } = [];
}
