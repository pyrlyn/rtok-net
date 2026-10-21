namespace Rtok.Plugin.Sdk;

/// <summary>
/// The frozen decision for one archived tool result. A tool result is shortened once; every
/// later turn replays the same pointer text, because context that changes byte for byte between
/// turns costs a cache miss on every one of them.
/// </summary>
/// <param name="ArchiveId">Handle for <see cref="IArchive.GetArchive"/> and for
/// <c>rtok expand &lt;id&gt;</c>.</param>
/// <param name="PointerText">The exact replacement text shown in place of the result.</param>
/// <param name="Expanded">Whether the user has already expanded this one.</param>
public sealed record ArchiveDecision(string ArchiveId, string PointerText, bool Expanded);
