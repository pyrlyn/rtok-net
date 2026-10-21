namespace Rtok.Store.Model;

/// <summary>Table <c>tokens</c>: a token count of one call phase.</summary>
public class TokenRow
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public long CallId { get; set; }
    public string? Plugin { get; set; }
    public string Phase { get; set; } = "";
    public string SourceName { get; set; } = "";
    public long Tokens { get; set; }
    public long? Bytes { get; set; }
    public long? Input { get; set; }
    public long? Output { get; set; }
    public long? CacheCreate { get; set; }
    public long? CacheRead { get; set; }
}
