namespace Rtok.Plugin.Sdk;

/// <summary>
/// One indexed symbol. The first six members are the graph edge; the rest is the byte span and
/// the text the definition search index stores.
/// </summary>
/// <param name="Name">Identifier as the tags query captured it.</param>
/// <param name="Kind">Tags syntax type (<c>function</c>, <c>struct</c>, <c>import</c>,
/// ...).</param>
/// <param name="Line">1-based line of the hit.</param>
/// <param name="IsDef">The hit is a definition.</param>
/// <param name="EndLine">1-based last line of the tagged node.</param>
/// <param name="Scope">Enclosing definition, or the full import specifier on an import
/// row.</param>
public sealed record SymbolRow(
    string Name,
    string Kind,
    int Line,
    bool IsDef,
    int EndLine,
    string Scope
)
{
    /// <summary>First byte of the hit in the file.</summary>
    public long StartByte { get; init; }

    /// <summary>One past the last byte of the hit.</summary>
    public long EndByte { get; init; }

    /// <summary>SHA-256 of the spanned bytes; empty when the span was not captured.</summary>
    public string ContentHash { get; init; } = "";

    /// <summary>The definition's source line; empty on a reference.</summary>
    public string Signature { get; init; } = "";

    /// <summary>Contiguous doc-comment lines directly above a definition, at most 512
    /// bytes.</summary>
    public string Doc { get; init; } = "";
}
