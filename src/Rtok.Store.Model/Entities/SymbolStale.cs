namespace Rtok.Store.Model;

/// <summary>Table <c>symbol_stale</c>: a path whose symbols need a reindex.</summary>
public class SymbolStale
{
    public string Root { get; set; } = "";
    public string Path { get; set; } = "";
}
