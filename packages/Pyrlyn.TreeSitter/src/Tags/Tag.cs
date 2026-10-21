namespace Pyrlyn.TreeSitter.Tags;

/// <summary>A definition or reference found by a <see cref="TagsConfiguration"/>.</summary>
/// <param name="Range">Bytes of the tagged node, widened to cover the name.</param>
/// <param name="NameRange">Bytes of the name.</param>
/// <param name="LineRange">
/// Bytes of the name's line, trimmed of ASCII whitespace and cut at 180 bytes.
/// </param>
/// <param name="SpanStart">Where the name starts.</param>
/// <param name="SpanEnd">Where the name ends.</param>
/// <param name="Utf16ColumnRange">The name's columns on its line, in UTF-16 code units.</param>
/// <param name="Docs">The doc comment, when the query captures one.</param>
/// <param name="IsDefinition">True for <c>@definition.*</c>, false for <c>@reference.*</c>.</param>
/// <param name="SyntaxTypeId">
/// The kind, such as <c>function</c> or <c>call</c>; see <see cref="TagsConfiguration.SyntaxTypeName"/>.
/// </param>
public sealed record Tag(
    TextRange Range,
    TextRange NameRange,
    TextRange LineRange,
    Point SpanStart,
    Point SpanEnd,
    TextRange Utf16ColumnRange,
    string? Docs,
    bool IsDefinition,
    int SyntaxTypeId
);
