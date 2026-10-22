using Rtok.Plugin.Sdk;

namespace Rtok.Store;

// The stored state other tasks own: the archive (T9), notes (T22), the read cache and the
// symbol index (T24, T25). Until they land a read misses, which every caller already handles,
// and a write throws, so the plugin guard logs the plugin instead of the plugin believing it
// stored something.
public sealed partial class StoreRuntime
{
    public string PutArchive(ReadOnlySpan<byte> body) => throw Pending("archive", "T9");

    public byte[]? GetArchive(string id) => null;

    public ArchiveDecision? GetArchiveDecision(string toolUseId) => null;

    public void PutArchiveDecision(string toolUseId, string archiveId, string pointerText) =>
        throw Pending("archive", "T9");

    public int MarkExpanded(string archiveId) => 0;

    public long UpsertNote(string? project, string kind, string title, string body) =>
        throw Pending("notes", "T22");

    public string? LatestNote(string kind) => null;

    public string? LatestNoteForProject(string? project, string kindPrefix) => null;

    public IReadOnlyList<(long Id, string Title)> ListNoteTitles(string? project, int limit) => [];

    public string? GetNoteBody(long id) => null;

    public IReadOnlyList<NoteHit> SearchNotes(string query, int limit) => [];

    // A cache may forget anything, so remembering nothing is a valid read cache.
    public void PutReadCache(string path, string sha256, string? archiveId) { }

    public (string? ArchiveId, long Timestamp)? GetReadCache(string path) => null;

    public void ClearReadCache(string path) { }

    public long SymbolCount(string root) => 0;

    public SymbolStat? GetSymbolStat(string root, string path) => null;

    public IReadOnlyDictionary<string, SymbolStat> SymbolStats(string root) =>
        new Dictionary<string, SymbolStat>(StringComparer.Ordinal);

    public void TouchSymbols(string root, string path, long mtime, long size) =>
        throw Pending("symbol index", "T24");

    public int ReplaceSymbols(
        string root,
        string path,
        string fileSha,
        long mtime,
        long size,
        IReadOnlyList<SymbolRow> rows
    ) => throw Pending("symbol index", "T24");

    public int ReplaceSymbolFiles(string root, IReadOnlyList<SymbolFile> files) =>
        throw Pending("symbol index", "T24");

    public int DeleteSymbolsMissing(string root, IReadOnlySet<string> keep) =>
        throw Pending("symbol index", "T24");

    public void MarkSymbolsStale(string absPath) { }

    public IReadOnlyList<(string Path, string Kind, int Line, int EndLine)> SymbolDefs(
        string root,
        string name
    ) => [];

    public IReadOnlyList<(string Path, string Kind, long Count, int Line)> SymbolRefGroups(
        string root,
        string name
    ) => [];

    public IReadOnlyList<(int Depth, string Path, string Name)> SymbolImpact(
        string root,
        string name,
        int depth
    ) => [];

    private static NotSupportedException Pending(string what, string task) =>
        new($"the store has no {what} yet ({task})");
}
