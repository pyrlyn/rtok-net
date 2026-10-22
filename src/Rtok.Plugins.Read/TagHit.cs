namespace Rtok.Plugins.Read;

/// <summary>One tags-query hit, a definition or a reference.</summary>
/// <param name="Kind">The tags syntax type: <c>function</c>, <c>call</c>, <c>import</c>, …</param>
/// <param name="Name">The captured name; on an import, the last segment of the specifier.</param>
/// <param name="Line">1-based line of the name.</param>
/// <param name="EndLine">1-based last line of the tagged node. A reference spans one line; a
/// definition covers its whole body.</param>
/// <param name="StartByte">First byte of the tagged node.</param>
/// <param name="EndByte">One past the last byte of the tagged node.</param>
/// <param name="IsDef">The hit is a definition.</param>
/// <param name="LineText">The name's line, trimmed and cut at 180 bytes.</param>
/// <param name="ImportPath">The full import specifier on an import row, else empty.</param>
public sealed record TagHit(
    string Kind,
    string Name,
    int Line,
    int EndLine,
    int StartByte,
    int EndByte,
    bool IsDef,
    string LineText,
    string ImportPath
);
