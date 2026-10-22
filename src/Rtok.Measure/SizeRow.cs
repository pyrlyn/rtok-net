namespace Rtok.Measure;

/// <summary>One row of a size table: tool results by tool, Bash family or MCP group.</summary>
internal sealed class SizeRow
{
    public long Count { get; set; }
    public long TotalBytes { get; set; }
    public long Mean { get; set; }
    public long P95 { get; set; }
    public long Max { get; set; }
    public long EstTokens { get; set; }

    /// <summary>Context-token-turns: each result's tokens times the turns it stayed in context.</summary>
    public long Ctt { get; set; }
}
