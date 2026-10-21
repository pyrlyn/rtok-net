namespace Rtok.Store.Model;

/// <summary>Table <c>hosts</c>: one row per agent host rtok knows (claude, codex, ...).</summary>
public class Host
{
    public long Id { get; set; }
    public string Slug { get; set; } = "";
    public string Kind { get; set; } = "";
    public long CreatedAt { get; set; }
}
