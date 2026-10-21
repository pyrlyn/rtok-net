namespace Rtok.Plugin.Sdk;

/// <summary>
/// The symbol index behind <c>symbol</c>, <c>callers</c> and <c>impact</c>: definitions and
/// references extracted from source, keyed by repository root and relative path. A cache the
/// host rebuilds from the files themselves, so a stale or empty index is a correctness-neutral
/// miss.
/// </summary>
public interface ISymbols
{
    /// <summary>How many symbols are indexed under <paramref name="root"/>; zero means not
    /// indexed yet.</summary>
    long SymbolCount(string root);

    /// <summary>The recorded stat of one file, if it has been indexed.</summary>
    SymbolStat? GetSymbolStat(string root, string path);

    /// <summary>Every indexed file's stat under <paramref name="root"/>, by relative
    /// path.</summary>
    IReadOnlyDictionary<string, SymbolStat> SymbolStats(string root);

    /// <summary>Update a file's mtime and size without re-parsing it: the content is
    /// unchanged.</summary>
    void TouchSymbols(string root, string path, long mtime, long size);

    /// <summary>Replace every symbol of one file in a single transaction; returns how many were
    /// written.</summary>
    int ReplaceSymbols(
        string root,
        string path,
        string fileSha,
        long mtime,
        long size,
        IReadOnlyList<SymbolRow> rows
    );

    /// <summary>Replace many files in one transaction (cold index).</summary>
    int ReplaceSymbolFiles(string root, IReadOnlyList<SymbolFile> files);

    /// <summary>Drop every indexed file under <paramref name="root"/> that is not in
    /// <paramref name="keep"/>; returns how many went.</summary>
    int DeleteSymbolsMissing(string root, IReadOnlySet<string> keep);

    /// <summary>Drop what was indexed for one absolute path: the file changed under the
    /// index.</summary>
    void MarkSymbolsStale(string absPath);

    /// <summary>Distinct indexed files under <paramref name="root"/>.</summary>
    long SymbolFileCount(string root) => 0;

    /// <summary>Pending re-index paths for <paramref name="root"/>.</summary>
    IReadOnlyList<string> SymbolPending(string root, string rootPath) => [];

    /// <summary>Unix seconds of the last successful index of <paramref name="root"/>.</summary>
    long? SymbolIndexedAt(string root) => null;

    /// <summary>Record the last successful index time of <paramref name="root"/>.</summary>
    void TouchSymbolIndexedAt(string root, long ts) { }

    /// <summary>Extractor fingerprint stored for <paramref name="root"/>, if any.</summary>
    string? ExtractorFingerprint(string root) => null;

    /// <summary>Record the extractor fingerprint for <paramref name="root"/> after a cold
    /// index.</summary>
    void SetExtractorFingerprint(string root, string fingerprint) { }

    /// <summary>Definitions of <paramref name="name"/>.</summary>
    IReadOnlyList<(string Path, string Kind, int Line, int EndLine)> SymbolDefs(
        string root,
        string name
    );

    /// <summary>References to <paramref name="name"/> grouped by location.</summary>
    IReadOnlyList<(string Path, string Kind, long Count, int Line)> SymbolRefGroups(
        string root,
        string name
    );

    /// <summary>Callees per definition of <paramref name="name"/>.</summary>
    IReadOnlyList<(string DefPath, int DefLine, string Callee, int FirstLine)> SymbolCallees(
        string root,
        string name
    ) => [];

    /// <summary>What <paramref name="name"/> reaches within <paramref name="depth"/>
    /// hops.</summary>
    IReadOnlyList<(int Depth, string Path, string Name)> SymbolImpact(
        string root,
        string name,
        int depth
    );

    /// <summary>Definitions with no same-name reference under <paramref name="root"/>.
    /// Name-based: a shared name keeps every same-named definition live.</summary>
    IReadOnlyList<(string Path, string Name, string Kind, int Line)> SymbolDeadCandidates(
        string root
    ) => [];

    /// <summary>Which of <paramref name="names"/> are referenced (non-import rows) under
    /// <paramref name="root"/>.</summary>
    IReadOnlySet<string> SymbolReferencedNames(string root, IReadOnlyList<string> names) =>
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Distinct definition names starting with <paramref name="prefix"/>, best
    /// <paramref name="limit"/> by reference count, ties by name.</summary>
    IReadOnlyList<string> SymbolNamePrefix(string root, string prefix, int limit) => [];

    /// <summary>Call chains from <paramref name="fromName"/> to <paramref name="toName"/> walked in the
    /// caller direction within <paramref name="depth"/> hops, shortest first.</summary>
    IReadOnlyList<string> SymbolPaths(string root, string fromName, string toName, int depth) => [];

    /// <summary>Definitions in <paramref name="path"/>; empty without an index.</summary>
    IReadOnlyList<(string Name, string Kind, int Line, int EndLine)> SymbolFileDefs(
        string root,
        string path
    ) => [];

    /// <summary>Import rows of <paramref name="path"/>.</summary>
    IReadOnlyList<(string Name, int Line)> SymbolImports(string root, string path) => [];

    /// <summary>Which definition file each referencing file imports, when
    /// <paramref name="name"/> has two or more definitions.</summary>
    IReadOnlyList<(string RefPath, string DefPath)> SymbolImportedDefs(string root, string name) =>
        [];

    /// <summary>Document frequency of <paramref name="name"/> and the number of indexed files;
    /// <c>(0, 0)</c> without an index.</summary>
    (long Df, long Files) SymbolNameFreq(string root, string name) => (0, 0);

    /// <summary>Recompute per-name document frequency after an index pass.</summary>
    void RebuildSymbolIdf(string root) { }

    /// <summary>Files that import <paramref name="moduleName"/>.</summary>
    IReadOnlyList<(string Path, int Line)> SymbolImporters(string root, string moduleName) => [];

    /// <summary>Definitions in files that import <paramref name="name"/>: the extra impact
    /// hop.</summary>
    IReadOnlyList<(string DefPath, string DefName)> SymbolImportFollow(string root, string name) =>
        [];

    /// <summary>Names ranked by reference count with one definition site, most referenced
    /// first, ties by name.</summary>
    IReadOnlyList<(string Name, long Refs, string Path, int Line)> SymbolTopRefs(
        string root,
        int limit
    ) => [];

    /// <summary>One row per name, path and definition flag with how many index rows it has,
    /// imports left out. The graph plugin builds its file graph from this one scan.</summary>
    IReadOnlyList<(string Name, string Path, bool IsDef, long Rows)> SymbolFileScan(string root) =>
        [];

    /// <summary>The file-graph document stored for <paramref name="root"/>, if any.</summary>
    string? FileRankGet(string root) => null;

    /// <summary>Replace the file-graph document of <paramref name="root"/>.</summary>
    void FileRankPut(string root, string graph) { }

    /// <summary>Byte span of one definition; <see langword="null"/> when that row is not
    /// indexed.</summary>
    SymbolSpan? GetSymbolSpan(string root, string path, string name, string kind, int line) => null;

    /// <summary>Definitions whose name, signature or doc matches <paramref name="query"/>: a
    /// name hit ranks above a signature hit, which ranks above a doc hit. At most
    /// <paramref name="limit"/> rows.</summary>
    IReadOnlyList<(string Path, string Name, string Kind, int Line)> SymbolFts(
        string root,
        string query,
        int limit
    ) => [];
}
