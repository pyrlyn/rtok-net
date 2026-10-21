namespace Rtok.Store.Model;

/// <summary>Table <c>agents</c>: one host session or sub-agent rtok sees.</summary>
public class Agent
{
    public string Id { get; set; } = "";
    public long HostId { get; set; }
    public string HostSessionId { get; set; } = "";
    public string ParentKey { get; set; } = "";
    public string? ParentId { get; set; }
    public string? Cwd { get; set; }
    public long StartedAt { get; set; }
    public long LastSeen { get; set; }
    public long? EndedAt { get; set; }
    public string? Activity { get; set; }
    public string? StatusText { get; set; }
    public string? Ancestors { get; set; }
}
