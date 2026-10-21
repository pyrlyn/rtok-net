namespace Rtok.Store.Model;

/// <summary>Table <c>archive_decisions</c>: a proxy pointer kept byte-stable per (session, tool_use_id).</summary>
public class ArchiveDecision
{
    public string Session { get; set; } = "";
    public string ToolUseId { get; set; } = "";
    public string ArchiveId { get; set; } = "";
    public string Pointer { get; set; } = "";
    public long? ExpandedTs { get; set; }
    public long Ts { get; set; }
}
