namespace Rtok.Store.Model;

/// <summary>Table <c>read_cache</c>: the last read of a path in a session.</summary>
public class ReadCacheEntry
{
    public string Session { get; set; } = "";
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Ts { get; set; }
    public string? ArchiveId { get; set; }
}
