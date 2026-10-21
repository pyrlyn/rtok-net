namespace Rtok.Store.Model;

/// <summary>Table <c>archive</c>: one content-addressed archived body; the file lives outside SQLite.</summary>
public class ArchiveEntry
{
    public string Id { get; set; } = "";
    public long Ts { get; set; }
    public string Session { get; set; } = "";
    public string? Tool { get; set; }
    public long Bytes { get; set; }
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string? AgentId { get; set; }
}
