namespace Rtok.Store.Model;

/// <summary>Table <c>observations</c>: something an agent did, recalled later.</summary>
public class Observation
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string SessionId { get; set; } = "";
    public string? Project { get; set; }
    public string ObsType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Narrative { get; set; } = "";
    public string Dedup { get; set; } = "";
    public string? CallKey { get; set; }
}
