namespace Rtok.Plugin.Sdk;

/// <summary>
/// Per-session memory of what has already been read, so the same file is not sent twice. A
/// cache: entries may be evicted, and the host scopes every member to the current session.
/// </summary>
public interface IReadCache
{
    /// <summary>Remember that <paramref name="path"/> was read, by content hash, optionally with
    /// the archived body.</summary>
    void PutReadCache(string path, string sha256, string? archiveId);

    /// <summary>The archive id and unix timestamp remembered for <paramref name="path"/>, if
    /// any.</summary>
    (string? ArchiveId, long Timestamp)? GetReadCache(string path);

    /// <summary>Forget <paramref name="path"/> and anything stored under it; call this when the
    /// file changes.</summary>
    void ClearReadCache(string path);

    /// <summary>The keys of everything remembered this session: a path, or a path followed by a
    /// tab and a mode.</summary>
    IReadOnlyList<string> ReadCacheKeys() => [];
}
