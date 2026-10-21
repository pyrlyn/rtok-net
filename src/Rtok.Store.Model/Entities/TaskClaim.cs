namespace Rtok.Store.Model;

/// <summary>Table <c>task_claims</c>: an agent's claim on a task.</summary>
public class TaskClaim
{
    public string Project { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string Title { get; set; } = "";
    public long Since { get; set; }
    public long Seq { get; set; }
}
