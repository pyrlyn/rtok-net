namespace Rtok.Store.Model;

/// <summary>Table <c>worktree_claims</c>: which agent a worktree is bound to.</summary>
public class WorktreeClaim
{
    public string Path { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string Task { get; set; } = "";
    public long ClaimedAt { get; set; }
    public long? ReleasedAt { get; set; }
}
