namespace Rtok.Store;

/// <summary>
/// One ingested doc item as a reader sees it. A projection, not the <c>DocItem</c> entity: EF
/// 10.0.13's query precompiler writes uncompilable code for a query that materializes that entity.
/// </summary>
public sealed record DocHit(long Id, string Path, string Kind, string Docs);
