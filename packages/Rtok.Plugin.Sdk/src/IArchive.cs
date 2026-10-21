namespace Rtok.Plugin.Sdk;

/// <summary>
/// Content-addressed blob storage, the other half of lossless: a plugin that shortens
/// something puts the original here first and quotes the returned id, so
/// <c>rtok expand &lt;id&gt;</c> can always give it back.
/// </summary>
public interface IArchive
{
    /// <summary>Store <paramref name="body"/> and return its handle. The same bytes always get
    /// the same id.</summary>
    string PutArchive(ReadOnlySpan<byte> body);

    /// <summary><see cref="PutArchive"/>, tagged with the context window that wrote it: a
    /// sub-agent's id, or <see langword="null"/> for the main window (see
    /// <see cref="ArchiveInSession"/>). The default drops the context.</summary>
    string PutArchiveFor(ReadOnlySpan<byte> body, string? context) => PutArchive(body);

    /// <summary>The bytes behind a handle, or <see langword="null"/> if the host no longer has
    /// them.</summary>
    byte[]? GetArchive(string id);

    /// <summary>The payload's size in bytes, or <see langword="null"/> if unknown. Metadata only:
    /// hot paths must not read a possibly-megabyte body.</summary>
    long? ArchiveSize(string id) => null;

    /// <summary>The decision already made for <paramref name="toolUseId"/>, if this result was
    /// shortened before.</summary>
    ArchiveDecision? GetArchiveDecision(string toolUseId);

    /// <summary>Persist the decision for <paramref name="toolUseId"/>. First writer wins: a
    /// recorded pointer stays exactly as it is, because the model pays a cache miss for every
    /// byte of context that moves between turns.</summary>
    void PutArchiveDecision(string toolUseId, string archiveId, string pointerText);

    /// <summary>Mark an archived blob as expanded by the user; returns how many rows
    /// changed.</summary>
    int MarkExpanded(string archiveId);

    /// <summary>The archive row for <paramref name="sha256"/> written in this session under
    /// <paramref name="context"/>. A body one context window archived is a hit only for that
    /// window: another never saw those bytes. The default finds nothing, so the caller prints
    /// the body.</summary>
    ArchiveHit? ArchiveInSession(string sha256, string? context) => null;

    /// <summary>The compressor's summary of <paramref name="archiveId"/> for compressor
    /// <paramref name="version"/>, if one was stored.</summary>
    string? ArchiveSummary(string archiveId, string version) => null;

    /// <summary>This session's archived tool results still in the live window, newest
    /// first.</summary>
    IReadOnlyList<(string ArchiveId, string Tool, long Bytes)> SessionLiveArchives(
        string session
    ) => [];
}
