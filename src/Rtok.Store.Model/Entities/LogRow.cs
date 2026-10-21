namespace Rtok.Store.Model;

/// <summary>Table <c>logs</c>: one log line mirrored into the store.</summary>
public class LogRow
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string Level { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Session { get; set; }
    public long? CallId { get; set; }
    public string? Plugin { get; set; }
    public string Message { get; set; } = "";
    public string? Fields { get; set; }
}
