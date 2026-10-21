namespace Rtok.Store.Model;

/// <summary>Table <c>sessions</c>: one host session; later writers never wipe what an earlier one set.</summary>
public class Session
{
    public string Id { get; set; } = "";
    public long? HostId { get; set; }
    public string? Project { get; set; }
    public string? Cwd { get; set; }
    public string? SourceName { get; set; }
    public long StartedAt { get; set; }
    public long? EndedAt { get; set; }
    public long EndSeq { get; set; }
}
