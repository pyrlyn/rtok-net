namespace Rtok.Store.Model;

/// <summary>Table <c>worktree_pending</c>: a worktree created for a task before an agent claims it.</summary>
public class WorktreePending
{
    public string Path { get; set; } = "";
    public string Task { get; set; } = "";
    public long CreatedAt { get; set; }
}
