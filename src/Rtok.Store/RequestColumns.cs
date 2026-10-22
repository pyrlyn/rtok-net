namespace Rtok.Store;

/// <summary>The request columns of one <c>call_io</c> row; top level, as EF's query precompiler
/// finds no nested type.</summary>
internal sealed record RequestColumns(
    byte[]? RequestRaw,
    string? RequestJson,
    string? RequestArchive
);
