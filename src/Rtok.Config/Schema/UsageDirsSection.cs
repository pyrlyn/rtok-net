namespace Rtok.Config;

/// <summary>where `rtok agents usage` reads each host's own records; Claude Code and Codex use [stats] transcripts_dir / codex_dir</summary>
/// <remarks>Read from [agents.usage.dirs].</remarks>
public sealed class UsageDirsSection
{
    /// <summary>the opencode*.db files in it; an untouched default follows $XDG_DATA_HOME</summary>
    public IList<string> Opencode { get; set; } = ["~/.local/share/opencode"];

    /// <summary>the kilo*.db files in it; an untouched default follows $XDG_DATA_HOME</summary>
    public IList<string> Kilo { get; set; } = ["~/.local/share/kilo"];

    /// <summary>*/events.jsonl; an untouched default follows $COPILOT_HOME</summary>
    public IList<string> Copilot { get; set; } = ["~/.copilot/session-state"];

    /// <summary>*/chats/session-*; an untouched default follows $GEMINI_CLI_HOME</summary>
    public IList<string> Gemini { get; set; } = ["~/.gemini/tmp"];

    /// <summary>listed as unsupported when present: Factory does not document the token fields</summary>
    public IList<string> Droid { get; set; } = ["~/.factory/sessions"];

    /// <summary>*/*.jsonl; an untouched default follows $PI_CODING_AGENT_SESSION_DIR, else $PI_CODING_AGENT_DIR/sessions</summary>
    public IList<string> Pi { get; set; } = ["~/.pi/agent/sessions"];

    /// <summary>Kimi Code: */*/agents/*/wire.jsonl; an untouched default follows $KIMI_CODE_HOME</summary>
    public IList<string> Kimi { get; set; } = ["~/.kimi-code/sessions"];

    /// <summary>listed as unsupported when present: xAI points at `grok usage`, which rtok does not run; follows $GROK_HOME</summary>
    public IList<string> Grok { get; set; } = ["~/.grok/sessions"];

    /// <summary>listed as unsupported when present: ZCode does not document its session records</summary>
    public IList<string> Zcode { get; set; } = ["~/.zcode"];

    /// <summary>listed as unsupported when present: Google does not document Antigravity's local data</summary>
    public IList<string> Antigravity { get; set; } = ["~/.gemini/antigravity"];
}
