namespace Rtok.Store.Model;

/// <summary>Table <c>symbols</c>: a definition or reference from tree-sitter tags, scoped per root.</summary>
public class Symbol
{
    public long Id { get; set; }
    public string Root { get; set; } = "";
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Line { get; set; }
    public long EndLine { get; set; }
    public long StartByte { get; set; }
    public long EndByte { get; set; }
    public bool IsDef { get; set; }
    public string Scope { get; set; } = "";
    public string FileSha { get; set; } = "";
    public long Mtime { get; set; }
    public long Size { get; set; }
    public string ContentHash { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Doc { get; set; } = "";
}
