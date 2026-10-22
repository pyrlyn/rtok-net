namespace Rtok.Store.Model;

/// <summary>
/// A row of the FTS5 table <c>doc_items_fts</c>, read only: <see cref="Query"/> is its
/// table-named hidden column, so <c>Query == q</c> is FTS5's <c>MATCH</c>, and
/// <see cref="Rank"/> is its <c>bm25()</c> rank. This lets EF write the search (README N7).
/// </summary>
public class DocItemMatch
{
    public long RowId { get; set; }

    /// <summary>The item this row indexes (<c>rowid</c> is its id).</summary>
    public DocItem Item { get; set; } = null!;

    public string Query { get; set; } = "";
    public double Rank { get; set; }
}
