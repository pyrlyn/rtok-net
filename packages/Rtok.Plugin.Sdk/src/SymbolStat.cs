namespace Rtok.Plugin.Sdk;

/// <summary>What the index recorded about one file, to tell whether it changed.</summary>
/// <param name="Sha">SHA-256 of the file at index time.</param>
/// <param name="Mtime">Modification time, unix seconds.</param>
/// <param name="Size">Size in bytes.</param>
public sealed record SymbolStat(string Sha, long Mtime, long Size);
