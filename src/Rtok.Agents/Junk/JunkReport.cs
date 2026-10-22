namespace Rtok.Agents.Junk;

/// <summary><c>rtok agents junk list</c>: per agent its folders, junk kinds and the space <c>clear</c> frees.</summary>
/// <param name="Agents">Largest <see cref="JunkAgent.FreedDefaultBytes"/> first.</param>
/// <param name="Own">rtok's own log and archive files.</param>
/// <param name="TotalBytes">Every folder once, however many agents share it.</param>
/// <param name="FreedDefaultBytes">What <c>clear</c> frees, each path once.</param>
/// <param name="FreedReviewBytes">What <c>clear --include review</c> frees.</param>
public sealed record JunkReport(
    IReadOnlyList<JunkAgent> Agents,
    IReadOnlyList<JunkOutcome> Own,
    long TotalBytes,
    long FreedDefaultBytes,
    long FreedReviewBytes
);
