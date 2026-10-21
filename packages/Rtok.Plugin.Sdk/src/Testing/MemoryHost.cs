using System.Security.Cryptography;
using System.Text.Json;

namespace Rtok.Plugin.Sdk.Testing;

/// <summary>
/// A host a plugin can run against without a database, for tests and examples. It keeps the
/// measurements a plugin records, the lines it logs, the blobs it archives and the archive
/// decisions, in memory, and answers every other capability empty.
/// </summary>
/// <remarks>
/// Empty is a legal answer, not a stub: a cache may be cold, a note absent, an index not built
/// yet, and a plugin has to survive all three. A plugin that needs a capability to hold state
/// implements the interfaces it needs itself; nothing about this host is privileged.
/// </remarks>
public sealed class MemoryHost : ICapabilities
{
    private readonly JsonElement? _config;
    private readonly Lock _gate = new();
    private readonly List<Measurement> _recorded = [];
    private readonly List<string> _logged = [];
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArchiveDecision> _decisions = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Sha256, string? Context), string> _session = [];

    /// <summary>A host with no configuration: every section a plugin asks for is
    /// <c>new T()</c>.</summary>
    public MemoryHost() { }

    /// <summary>A host whose configuration is <paramref name="configJson"/>, shaped like the
    /// host's own file: <c>{"plugins": {"mine": {"enabled": true}}}</c> is what
    /// <c>PluginConfig("mine", ...)</c> reads.</summary>
    public MemoryHost(string configJson)
    {
        using var doc = JsonDocument.Parse(configJson);
        _config = doc.RootElement.Clone();
    }

    /// <inheritdoc/>
    public string Session => "memory";

    /// <summary>Every <see cref="Measurement"/> recorded so far, in order.</summary>
    public IReadOnlyList<Measurement> Recorded()
    {
        lock (_gate)
        {
            return [.. _recorded];
        }
    }

    /// <summary>Every log line so far, formatted <c>level source/name: message</c>.</summary>
    public IReadOnlyList<string> Logged()
    {
        lock (_gate)
        {
            return [.. _logged];
        }
    }

    /// <summary>The crude version of the host's calibrated rate table, so a plugin's arithmetic
    /// can be asserted without a tokenizer.</summary>
    public int Estimate(string text, TextClass textClass)
    {
        ArgumentNullException.ThrowIfNull(text);
        var perToken = textClass switch
        {
            TextClass.Prose => 4.0,
            TextClass.Json => 2.8,
            TextClass.Cjk => 1.0,
            _ => 3.5,
        };
        var chars = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            chars++;
        }

        return (int)Math.Ceiling(chars / perToken);
    }

    /// <inheritdoc/>
    public void Record(Measurement measurement)
    {
        lock (_gate)
        {
            _recorded.Add(measurement);
        }
    }

    /// <inheritdoc/>
    public long RecordCall(string surface, string kind, string? name) => 0;

    /// <inheritdoc/>
    public long RecordPluginRun(string surface, string plugin) => 0;

    /// <inheritdoc/>
    public void RecordTokens(
        long callId,
        string? plugin,
        string phase,
        string source,
        long tokens
    ) { }

    /// <inheritdoc/>
    public void Log(string level, string source, string name, string message)
    {
        lock (_gate)
        {
            _logged.Add($"{level} {source}/{name}: {message}");
        }
    }

    /// <inheritdoc/>
    public JsonElement? ConfigJson(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_config is not { } node)
        {
            return null;
        }

        foreach (var key in path.Split('.'))
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(key, out node))
            {
                return null;
            }
        }

        return node;
    }

    /// <summary>When set, every archive write throws <see cref="IOException"/>, as a full or
    /// broken store does.</summary>
    public bool ArchiveFails { get; set; }

    /// <summary>Content-addressed: the same bytes always get the same id, and two different
    /// bodies never share one.</summary>
    public string PutArchive(ReadOnlySpan<byte> body)
    {
        if (ArchiveFails)
        {
            throw new IOException("archive write failed");
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(body, hash);
        var id = "mem" + Convert.ToHexStringLower(hash[..8]);
        var copy = body.ToArray();
        lock (_gate)
        {
            _blobs[id] = copy;
        }

        return id;
    }

    /// <summary><see cref="PutArchive"/>, remembered for <see cref="ArchiveInSession"/> under
    /// <paramref name="context"/>.</summary>
    public string PutArchiveFor(ReadOnlySpan<byte> body, string? context)
    {
        var id = PutArchive(body);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(body));
        lock (_gate)
        {
            _session[(sha256, context)] = id;
        }

        return id;
    }

    /// <summary>A body <see cref="PutArchiveFor"/> stored under the same
    /// <paramref name="context"/>, one turn ago.</summary>
    public ArchiveHit? ArchiveInSession(string sha256, string? context)
    {
        lock (_gate)
        {
            return _session.TryGetValue((sha256, context), out var id)
                ? new ArchiveHit(id, 1)
                : null;
        }
    }

    /// <inheritdoc/>
    public byte[]? GetArchive(string id)
    {
        lock (_gate)
        {
            return _blobs.TryGetValue(id, out var body) ? [.. body] : null;
        }
    }

    /// <inheritdoc/>
    public long? ArchiveSize(string id)
    {
        lock (_gate)
        {
            return _blobs.TryGetValue(id, out var body) ? body.LongLength : null;
        }
    }

    /// <inheritdoc/>
    public ArchiveDecision? GetArchiveDecision(string toolUseId)
    {
        lock (_gate)
        {
            return _decisions.GetValueOrDefault(toolUseId);
        }
    }

    /// <inheritdoc/>
    public void PutArchiveDecision(string toolUseId, string archiveId, string pointerText)
    {
        lock (_gate)
        {
            _decisions.TryAdd(toolUseId, new ArchiveDecision(archiveId, pointerText, false));
        }
    }

    /// <inheritdoc/>
    public int MarkExpanded(string archiveId)
    {
        lock (_gate)
        {
            var hits = _decisions
                .Where(d => string.Equals(d.Value.ArchiveId, archiveId, StringComparison.Ordinal))
                .Select(d => d.Key)
                .ToList();
            foreach (var key in hits)
            {
                _decisions[key] = _decisions[key] with { Expanded = true };
            }

            return hits.Count;
        }
    }

    /// <inheritdoc/>
    public long UpsertNote(string? project, string kind, string title, string body) => 0;

    /// <inheritdoc/>
    public string? LatestNote(string kind) => null;

    /// <inheritdoc/>
    public string? LatestNoteForProject(string? project, string kindPrefix) => null;

    /// <inheritdoc/>
    public IReadOnlyList<(long Id, string Title)> ListNoteTitles(string? project, int limit) => [];

    /// <inheritdoc/>
    public string? GetNoteBody(long id) => null;

    /// <inheritdoc/>
    public IReadOnlyList<NoteHit> SearchNotes(string query, int limit) => [];

    /// <inheritdoc/>
    public void PutReadCache(string path, string sha256, string? archiveId) { }

    /// <inheritdoc/>
    public (string? ArchiveId, long Timestamp)? GetReadCache(string path) => null;

    /// <inheritdoc/>
    public void ClearReadCache(string path) { }

    /// <inheritdoc/>
    public IReadOnlyList<string> RecentHookInputs(int limit) => [];

    /// <inheritdoc/>
    public long CallsSince(long ts) => 0;

    /// <inheritdoc/>
    public string? LastMeasurementRef(string plugin, string kind) => null;

    /// <inheritdoc/>
    public long SymbolCount(string root) => 0;

    /// <inheritdoc/>
    public SymbolStat? GetSymbolStat(string root, string path) => null;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, SymbolStat> SymbolStats(string root) =>
        new Dictionary<string, SymbolStat>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void TouchSymbols(string root, string path, long mtime, long size) { }

    /// <inheritdoc/>
    public int ReplaceSymbols(
        string root,
        string path,
        string fileSha,
        long mtime,
        long size,
        IReadOnlyList<SymbolRow> rows
    ) => 0;

    /// <inheritdoc/>
    public int ReplaceSymbolFiles(string root, IReadOnlyList<SymbolFile> files) => 0;

    /// <inheritdoc/>
    public int DeleteSymbolsMissing(string root, IReadOnlySet<string> keep) => 0;

    /// <inheritdoc/>
    public void MarkSymbolsStale(string absPath) { }

    /// <inheritdoc/>
    public IReadOnlyList<(string Path, string Kind, int Line, int EndLine)> SymbolDefs(
        string root,
        string name
    ) => [];

    /// <inheritdoc/>
    public IReadOnlyList<(string Path, string Kind, long Count, int Line)> SymbolRefGroups(
        string root,
        string name
    ) => [];

    /// <inheritdoc/>
    public IReadOnlyList<(int Depth, string Path, string Name)> SymbolImpact(
        string root,
        string name,
        int depth
    ) => [];
}
