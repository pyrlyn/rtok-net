namespace Rtok.Agents.Junk;

/// <summary>
/// One path <c>clear</c> may remove, with the evidence D36 accepts as the reason it is junk and,
/// when <c>clear</c> leaves it for now, why. A kept item is listed but counted in no total.
/// </summary>
/// <param name="Kind">The junk kind: <c>cache</c>, <c>temp</c>, <c>logs</c> …</param>
/// <param name="Class"><c>safe</c>, <c>review</c>, <c>explicit</c> or <c>never</c>.</param>
/// <param name="Path">The path.</param>
/// <param name="Bytes">Allocated bytes, a lower bound when the walk hit its limit.</param>
/// <param name="Evidence">Why it is junk: <see cref="JunkEvidence"/>.</param>
/// <param name="Kept">Why it stays, or null when <c>clear</c> takes it.</param>
public sealed record JunkItem(
    string Kind,
    string Class,
    string Path,
    long Bytes,
    string Evidence,
    string? Kept
)
{
    /// <summary>Part of the totals: nothing keeps it.</summary>
    public bool Counted => Kept is null;
}
