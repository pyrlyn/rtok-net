namespace Rtok.Store.Model;

/// <summary>Table <c>doc_items</c>: one documented item of a package.</summary>
public class DocItem
{
    public long Id { get; set; }
    public string CrateName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Docs { get; set; } = "";
}
