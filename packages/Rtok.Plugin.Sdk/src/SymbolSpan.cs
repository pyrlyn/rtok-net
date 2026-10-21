namespace Rtok.Plugin.Sdk;

/// <summary>Byte range of one indexed definition, and the hashes that say the file still
/// matches.</summary>
/// <param name="StartByte">First byte of the definition in the file.</param>
/// <param name="EndByte">One past the last byte of the definition.</param>
/// <param name="ContentHash">SHA-256 of the spanned bytes.</param>
/// <param name="FileSha">SHA-256 of the whole file at index time.</param>
public sealed record SymbolSpan(long StartByte, long EndByte, string ContentHash, string FileSha);
