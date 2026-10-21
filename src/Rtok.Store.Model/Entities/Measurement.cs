namespace Rtok.Store.Model;

/// <summary>Table <c>measurements</c>: one transformation and its saving; once_key makes a write idempotent.</summary>
public class Measurement
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string Session { get; set; } = "";
    public string Plugin { get; set; } = "";
    public string Kind { get; set; } = "";
    public long BeforeBytes { get; set; }
    public long AfterBytes { get; set; }
    public long EstBefore { get; set; }
    public long EstAfter { get; set; }
    public string? RefId { get; set; }
    public long? CallId { get; set; }
    public string? OnceKey { get; set; }
}
