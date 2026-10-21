namespace Rtok.Plugin.Sdk;

/// <summary>A same-session archive row whose SHA-256 matched a later payload.</summary>
/// <param name="Id">Handle for <see cref="IArchive.GetArchive"/> and for
/// <c>rtok expand &lt;id&gt;</c>.</param>
/// <param name="Turns">Measurement rows in this session after the original archive; 0 if none
/// yet.</param>
public sealed record ArchiveHit(string Id, long Turns);
