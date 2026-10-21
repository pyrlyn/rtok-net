namespace Rtok.Store.Model;

/// <summary>Table <c>calls</c>: one hook dispatch, MCP call, API request or plugin run.</summary>
public class CallRow
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string SessionId { get; set; } = "";
    public long? HostId { get; set; }
    public long? ProviderId { get; set; }
    public long? ModelId { get; set; }
    public string? Plugin { get; set; }
    public string Surface { get; set; } = "";
    public string Kind { get; set; } = "";
    public long? ParentId { get; set; }
    public string? Name { get; set; }
    public double? Ms { get; set; }
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? ServiceTier { get; set; }
}
