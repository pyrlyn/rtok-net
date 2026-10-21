namespace Rtok.Store.Model;

/// <summary>Table <c>graph_events</c>: one graph tool call phase the web page streams.</summary>
public class GraphEvent
{
    public long Id { get; set; }
    public long TsMs { get; set; }
    public string Call { get; set; } = "";
    public string Phase { get; set; } = "";
    public string Session { get; set; } = "";
    public string Tool { get; set; } = "";
    public string? Target { get; set; }
    public string? Project { get; set; }
    public string? Backend { get; set; }
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public double? Ms { get; set; }
    public long? Done { get; set; }
    public long? Total { get; set; }
    public long? AnswerTokens { get; set; }
    public string? RowsJson { get; set; }
    public long? Symbols { get; set; }
    public long? SymbolsReturned { get; set; }
    public long? FilesTouched { get; set; }
    public long? ProjectsHit { get; set; }
}
