namespace Rtok.Agents.Junk;

/// <summary>One agent row of the report.</summary>
/// <param name="Name">A host id, or <c>rtok</c>.</param>
/// <param name="Host">A host; rtok itself is the one row that is not.</param>
/// <param name="Installed">Installed here.</param>
/// <param name="Folders">Its folders.</param>
/// <param name="TotalBytes">Their usage, a nested folder counted once.</param>
/// <param name="Kinds">Kind rows of the counted items.</param>
/// <param name="Items">Every item, counted or kept.</param>
/// <param name="Notes">Lines printed under the kinds.</param>
/// <param name="FreedDefaultBytes">What <c>clear</c> frees.</param>
/// <param name="FreedReviewBytes">What <c>clear --include review</c> frees.</param>
public sealed record JunkAgent(
    string Name,
    bool Host,
    bool Installed,
    IReadOnlyList<JunkFolder> Folders,
    long TotalBytes,
    IReadOnlyList<JunkKindRow> Kinds,
    IReadOnlyList<JunkItem> Items,
    IReadOnlyList<string> Notes,
    long FreedDefaultBytes,
    long FreedReviewBytes
);
