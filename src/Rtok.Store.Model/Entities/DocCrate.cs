namespace Rtok.Store.Model;

/// <summary>Table <c>doc_crates</c>: a fetched documentation package.</summary>
public class DocCrate
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long FormatVersion { get; set; }
    public long FetchedUnix { get; set; }
}
