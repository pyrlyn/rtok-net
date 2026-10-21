namespace Rtok.Store.Model;

/// <summary>Table <c>extractor</c>: the extractor fingerprint a root was indexed with.</summary>
public class Extractor
{
    public string Root { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public long? IndexedAt { get; set; }
}
