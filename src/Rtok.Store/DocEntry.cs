namespace Rtok.Store;

/// <summary>One documented item to ingest: its path (<c>serde::Deserialize</c>), kind and body.</summary>
public sealed record DocEntry(string Path, string Kind, string Docs);
