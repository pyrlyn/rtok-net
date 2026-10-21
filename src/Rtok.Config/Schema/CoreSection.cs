namespace Rtok.Config;

/// <summary>paths shared by every surface</summary>
/// <remarks>Read from [core].</remarks>
public sealed class CoreSection
{
    /// <summary>false = plain proxy (no business logic); HTTP stays up until process exit</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>one SQLite file, WAL</summary>
    public string DbPath { get; set; } = "~/.rtok/rtok.db";

    /// <summary>raw payloads for `rtok expand &lt;id&gt;`</summary>
    public string ArchiveDir { get; set; } = "~/.rtok/archive";

    /// <summary>env var consulted for the session id when stdin has none</summary>
    public string SessionEnv { get; set; } = "CLAUDE_SESSION_ID";

    /// <summary>MCP/API bodies larger than this go to archive (hooks never archive)</summary>
    public uint CallIoInlineBytes { get; set; } = 65536;

    /// <summary>rtok hook &lt;event&gt; stdin cap (8 MiB); over it, exits 0 unmodified, no archiving or hashing</summary>
    public uint HookMaxInputBytes { get; set; } = 8388608;

    /// <summary>0 = keep `calls` forever</summary>
    public uint RetainCallsDays { get; set; } = 30;

    /// <summary>hook stdin bodies cleared after N days, rows kept; 0 = as long as `calls`</summary>
    public uint RetainHookBodiesDays { get; set; } = 3;

    /// <summary>true = save request bodies verbatim; false strips escapes, control chars, harness wrappers, trailing blanks</summary>
    public bool StoreRaw { get; set; }
}
