using System.Text.Json;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;
using Rtok.Store;

namespace Rtok.Plugins.Graph.Tests;

/// <summary>A host whose symbol index is a real <see cref="RtokStore"/>; everything else is a
/// <see cref="MemoryHost"/> with <paramref name="configJson"/> as its configuration.</summary>
internal sealed class StoreHost(RtokStore store, string? configJson = null) : ICapabilities
{
    private readonly MemoryHost _memory = configJson is null ? new() : new(configJson);
    private readonly ISymbols _symbols = store;

    public MemoryHost Memory => _memory;

    public string? Cwd { get; init; }

    private readonly Dictionary<string, IReadOnlyList<string>> _watchPending = new(
        StringComparer.Ordinal
    );

    public IReadOnlyList<string> GraphWatchPending(string root)
    {
        lock (_watchPending)
        {
            return _watchPending.GetValueOrDefault(root, []);
        }
    }

    public void PublishGraphWatchPending(string root, IReadOnlyList<string> paths)
    {
        lock (_watchPending)
        {
            _watchPending[root] = [.. paths];
        }
    }

    public string? PluginStateGet(string plugin, string key) => store.PluginStateGet(plugin, key);

    public void PluginStateSet(string plugin, string key, string value) =>
        store.PluginStateSet(plugin, key, value);

    public string Session => _memory.Session;

    public int Estimate(string text, TextClass textClass) => _memory.Estimate(text, textClass);

    public void Record(Measurement measurement) => _memory.Record(measurement);

    public long RecordCall(string surface, string kind, string? name) =>
        _memory.RecordCall(surface, kind, name);

    public long RecordPluginRun(string surface, string plugin) =>
        _memory.RecordPluginRun(surface, plugin);

    public void RecordTokens(
        long callId,
        string? plugin,
        string phase,
        string source,
        long tokens
    ) => _memory.RecordTokens(callId, plugin, phase, source, tokens);

    public void Log(string level, string source, string name, string message) =>
        _memory.Log(level, source, name, message);

    public JsonElement? ConfigJson(string path) => _memory.ConfigJson(path);

    /// <summary>Every archive id handed out, in order.</summary>
    public List<string> Archived { get; } = [];

    public string PutArchive(ReadOnlySpan<byte> body)
    {
        var id = _memory.PutArchive(body);
        Archived.Add(id);
        return id;
    }

    /// <summary>The archive a printed id prefix names, as <c>expand</c> resolves it: exactly one
    /// handed-out id must start with it.</summary>
    public byte[] Expand(string shown)
    {
        // Framework-free: tests/Rtok.Benchmarks links this file too.
        if (shown.Length != ArchiveIds.ShortLength)
        {
            throw new ArgumentException($"{shown}: not a printed archive id", nameof(shown));
        }

        var ids = Archived
            .Distinct(StringComparer.Ordinal)
            .Where(a => a.StartsWith(shown, StringComparison.Ordinal))
            .ToList();
        return ids.Count == 1
            ? _memory.GetArchive(ids[0]) ?? throw new InvalidOperationException(ids[0])
            : throw new InvalidOperationException($"{shown} names {ids.Count} archives");
    }

    public byte[]? GetArchive(string id) => _memory.GetArchive(id);

    public ArchiveDecision? GetArchiveDecision(string toolUseId) =>
        _memory.GetArchiveDecision(toolUseId);

    public void PutArchiveDecision(string toolUseId, string archiveId, string pointerText) =>
        _memory.PutArchiveDecision(toolUseId, archiveId, pointerText);

    public int MarkExpanded(string archiveId) => _memory.MarkExpanded(archiveId);

    public long UpsertNote(string? project, string kind, string title, string body) => 0;

    public string? LatestNote(string kind) => null;

    public string? LatestNoteForProject(string? project, string kindPrefix) => null;

    public IReadOnlyList<(long Id, string Title)> ListNoteTitles(string? project, int limit) => [];

    public string? GetNoteBody(long id) => null;

    public IReadOnlyList<NoteHit> SearchNotes(string query, int limit) => [];

    public void PutReadCache(string path, string sha256, string? archiveId) { }

    public (string? ArchiveId, long Timestamp)? GetReadCache(string path) => null;

    public void ClearReadCache(string path) { }

    public IReadOnlyList<string> RecentHookInputs(int limit) => [];

    public long CallsSince(long ts) => 0;

    public string? LastMeasurementRef(string plugin, string kind) => null;

    public long SymbolCount(string root) => _symbols.SymbolCount(root);

    public SymbolStat? GetSymbolStat(string root, string path) =>
        _symbols.GetSymbolStat(root, path);

    public IReadOnlyDictionary<string, SymbolStat> SymbolStats(string root) =>
        _symbols.SymbolStats(root);

    public void TouchSymbols(string root, string path, long mtime, long size) =>
        _symbols.TouchSymbols(root, path, mtime, size);

    public int ReplaceSymbols(
        string root,
        string path,
        string fileSha,
        long mtime,
        long size,
        IReadOnlyList<SymbolRow> rows
    ) => _symbols.ReplaceSymbols(root, path, fileSha, mtime, size, rows);

    /// <summary>The file count of every <see cref="ReplaceSymbolFiles"/> call: one per transaction.</summary>
    public List<int> Batches { get; } = [];

    public int ReplaceSymbolFiles(string root, IReadOnlyList<SymbolFile> files)
    {
        Batches.Add(files.Count);
        return _symbols.ReplaceSymbolFiles(root, files);
    }

    public int DeleteSymbolsMissing(string root, IReadOnlySet<string> keep) =>
        _symbols.DeleteSymbolsMissing(root, keep);

    public void MarkSymbolsStale(string absPath) => _symbols.MarkSymbolsStale(absPath);

    public long SymbolFileCount(string root) => _symbols.SymbolFileCount(root);

    public IReadOnlyList<string> SymbolPending(string root, string rootPath) =>
        _symbols.SymbolPending(root, rootPath);

    public long? SymbolIndexedAt(string root) => _symbols.SymbolIndexedAt(root);

    public void TouchSymbolIndexedAt(string root, long ts) =>
        _symbols.TouchSymbolIndexedAt(root, ts);

    public string? ExtractorFingerprint(string root) => _symbols.ExtractorFingerprint(root);

    public void SetExtractorFingerprint(string root, string fingerprint) =>
        _symbols.SetExtractorFingerprint(root, fingerprint);

    public IReadOnlyList<(string Path, string Kind, int Line, int EndLine)> SymbolDefs(
        string root,
        string name
    ) => _symbols.SymbolDefs(root, name);

    public IReadOnlyList<(string Path, string Kind, long Count, int Line)> SymbolRefGroups(
        string root,
        string name
    ) => _symbols.SymbolRefGroups(root, name);

    public IReadOnlyList<(string DefPath, int DefLine, string Callee, int FirstLine)> SymbolCallees(
        string root,
        string name
    ) => _symbols.SymbolCallees(root, name);

    public IReadOnlyList<(int Depth, string Path, string Name)> SymbolImpact(
        string root,
        string name,
        int depth
    ) => _symbols.SymbolImpact(root, name, depth);

    public IReadOnlyList<(string Path, string Name, string Kind, int Line)> SymbolDeadCandidates(
        string root
    ) => _symbols.SymbolDeadCandidates(root);

    public IReadOnlySet<string> SymbolReferencedNames(string root, IReadOnlyList<string> names) =>
        _symbols.SymbolReferencedNames(root, names);

    public IReadOnlyList<string> SymbolNamePrefix(string root, string prefix, int limit) =>
        _symbols.SymbolNamePrefix(root, prefix, limit);

    public IReadOnlyList<string> SymbolPaths(
        string root,
        string fromName,
        string toName,
        int depth
    ) => _symbols.SymbolPaths(root, fromName, toName, depth);

    public IReadOnlyList<(string Name, string Kind, int Line, int EndLine)> SymbolFileDefs(
        string root,
        string path
    ) => _symbols.SymbolFileDefs(root, path);

    public IReadOnlyList<(string Name, int Line)> SymbolImports(string root, string path) =>
        _symbols.SymbolImports(root, path);

    public IReadOnlyList<(string RefPath, string DefPath)> SymbolImportedDefs(
        string root,
        string name
    ) => _symbols.SymbolImportedDefs(root, name);

    public (long Df, long Files) SymbolNameFreq(string root, string name) =>
        _symbols.SymbolNameFreq(root, name);

    public void RebuildSymbolIdf(string root) => _symbols.RebuildSymbolIdf(root);

    public IReadOnlyList<(string Path, int Line)> SymbolImporters(string root, string moduleName) =>
        _symbols.SymbolImporters(root, moduleName);

    public IReadOnlyList<(string DefPath, string DefName)> SymbolImportFollow(
        string root,
        string name
    ) => _symbols.SymbolImportFollow(root, name);

    public IReadOnlyList<(string Name, long Refs, string Path, int Line)> SymbolTopRefs(
        string root,
        int limit
    ) => _symbols.SymbolTopRefs(root, limit);

    public IReadOnlyList<(string Name, string Path, bool IsDef, long Rows)> SymbolFileScan(
        string root
    ) => _symbols.SymbolFileScan(root);

    public string? FileRankGet(string root) => _symbols.FileRankGet(root);

    public void FileRankPut(string root, string graph) => _symbols.FileRankPut(root, graph);

    public SymbolSpan? GetSymbolSpan(
        string root,
        string path,
        string name,
        string kind,
        int line
    ) => _symbols.GetSymbolSpan(root, path, name, kind, line);

    public IReadOnlyList<(string Path, string Name, string Kind, int Line)> SymbolFts(
        string root,
        string query,
        int limit
    ) => _symbols.SymbolFts(root, query, limit);
}
