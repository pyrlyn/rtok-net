namespace Rtok.Config;

/// <summary>session memory and recall</summary>
/// <remarks>Read from [plugins.memory].</remarks>
public sealed class MemorySection
{
    /// <summary>session memory and recall</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>SessionStart: last N titles + ids</summary>
    public uint RecallTitles { get; set; } = 5;

    /// <summary>token budget of the recall block</summary>
    public uint RecallTokens { get; set; } = 200;

    /// <summary>UserPromptSubmit: 0 = off; N = ranked titles per turn</summary>
    public uint PromptRecall { get; set; } = 5;

    /// <summary>PreCompact → SessionStart(compact)</summary>
    public uint CheckpointTokens { get; set; } = 400;

    /// <summary>memory search hit ceiling</summary>
    public uint SearchLimit { get; set; } = 5;

    /// <summary>rtok memory sync: CLAUDE.md / AGENTS.md block</summary>
    public uint SyncTokens { get; set; } = 300;

    /// <summary>SessionStart(startup) restores newest session:* note</summary>
    public bool StartupRecall { get; set; } = true;

    /// <summary>sub-agent digest MCP tool</summary>
    public bool Handoff { get; set; } = true;

    /// <summary>SubagentStart pointer digest</summary>
    public bool SpawnBrief { get; set; } = true;

    /// <summary>token budget for the spawn brief</summary>
    public uint SpawnBriefTokens { get; set; } = 300;

    /// <summary>mem_save refuses a too-short or duplicate note</summary>
    public bool Admit { get; set; } = true;

    /// <summary>shortest body the gate admits</summary>
    public uint AdmitMinChars { get; set; } = 5;

    /// <summary>percent trigram overlap that counts as a duplicate</summary>
    public uint AdmitSimilarity { get; set; } = 85;

    /// <summary>`memory forget` retires notes unread this long; 0 = needs --days</summary>
    public uint ForgetAfterDays { get; set; } = 90;

    /// <summary>The [plugins.memory.embed] table.</summary>
    public MemoryEmbedSection Embed { get; set; } = new();

    /// <summary>The [plugins.memory.extract] table.</summary>
    public MemoryExtractSection Extract { get; set; } = new();
}
