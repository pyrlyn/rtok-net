namespace Rtok.Agents.Junk;

/// <summary>One folder an agent writes to, with its disk usage.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Role"><c>data</c>, <c>temp</c>, <c>logs</c> or <c>cache</c>.</param>
/// <param name="SizeBytes">Allocated bytes.</param>
/// <param name="Documented">A §22 row backs it; anything else is listed read-only.</param>
/// <param name="SharedWith">The other agents whose folder is this same directory; it is sized once.</param>
/// <param name="Note">Why the size may be low, or that it is a symlink.</param>
/// <param name="Key">The real directory it was sized as.</param>
public sealed record JunkFolder(
    string Path,
    string Role,
    long SizeBytes,
    bool Documented,
    IReadOnlyList<string> SharedWith,
    string? Note,
    string Key
);
