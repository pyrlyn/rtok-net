namespace Rtok.Config;

/// <summary>repeated or wasteful tool calls</summary>
/// <remarks>Read from [plugins.guard].</remarks>
public sealed class GuardSection
{
    /// <summary>deny repeated or wasteful tool calls</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>turns a repeated call is remembered</summary>
    public uint WindowTurns { get; set; } = 8;

    /// <summary>opt-in: deny native Grep/Glob, point at MCP search/tree</summary>
    public bool DenyGrepGlob { get; set; }

    /// <summary>opt-in: answer a Grep for one identifier with its indexed definitions</summary>
    public bool GrepSymbol { get; set; }

    /// <summary>Opt-in: on `PreToolUse(Skill)` a `SKILL.md` over `skill_max_bytes` is archived and denied with its markdown map plus the `expand` pointer, unless its frontmatter carries a key the host applies on invocation</summary>
    public bool Skills { get; set; }

    /// <summary>skill bodies above this are flagged</summary>
    public uint SkillMaxBytes { get; set; } = 8192;
}
