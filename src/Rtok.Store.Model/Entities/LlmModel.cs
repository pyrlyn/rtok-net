namespace Rtok.Store.Model;

/// <summary>Table <c>models</c>: one model slug of a provider.</summary>
public class LlmModel
{
    public long Id { get; set; }
    public long ProviderId { get; set; }
    public string Slug { get; set; } = "";
    public long CreatedAt { get; set; }
}
