namespace Rtok.Plugin.Sdk;

/// <summary>One file of a batched cold-index write.</summary>
/// <param name="Path">Root-relative path.</param>
/// <param name="Sha">SHA-256 of the file.</param>
/// <param name="Mtime">Modification time, nanoseconds since the Unix epoch (<see cref="SymbolStat.Stamp"/>).</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Rows">Every symbol of the file.</param>
public sealed record SymbolFile(
    string Path,
    string Sha,
    long Mtime,
    long Size,
    IReadOnlyList<SymbolRow> Rows
);
