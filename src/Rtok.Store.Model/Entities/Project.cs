namespace Rtok.Store.Model;

/// <summary>Table <c>projects</c>: a graph project root.</summary>
public class Project
{
    public long Id { get; set; }
    public string Root { get; set; } = "";
    public string? Name { get; set; }
    public string Origin { get; set; } = "";
    public long CreatedAt { get; set; }
    public long LastUsedAt { get; set; }
    public bool Selected { get; set; }
}
