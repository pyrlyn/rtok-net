namespace Rtok.Store.Model;

/// <summary>Table <c>usage</c>: provider ground truth, one row per API request.</summary>
public class Usage
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string Session { get; set; } = "";
    public string? Model { get; set; }
    public long Input { get; set; }
    public long CacheCreate { get; set; }
    public long CacheRead { get; set; }
    public long Output { get; set; }
    public long? CallId { get; set; }
    public string Api { get; set; } = "";
    public string? ResultKey { get; set; }
}
