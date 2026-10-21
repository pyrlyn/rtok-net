namespace Rtok.Store.Model;

/// <summary>Table <c>project_links</c>: a link between two projects.</summary>
public class ProjectLink
{
    public long FromId { get; set; }
    public long ToId { get; set; }
    public string Kind { get; set; } = "";
    public string? Reason { get; set; }
    public bool Unlinked { get; set; }
    public long CreatedAt { get; set; }
}
