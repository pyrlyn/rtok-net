namespace Rtok.Store.Model;

/// <summary>Table <c>providers</c>: an LLM API provider.</summary>
public class Provider
{
    public long Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public long CreatedAt { get; set; }
}
