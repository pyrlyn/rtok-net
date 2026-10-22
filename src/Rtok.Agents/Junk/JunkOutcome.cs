namespace Rtok.Agents.Junk;

/// <summary>One of rtok's own files <c>clear</c> would remove: a log sibling or an archive.</summary>
/// <param name="Kind"><c>log</c> or <c>archive</c>.</param>
/// <param name="Path">The file.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Note">Why it is junk.</param>
public sealed record JunkOutcome(string Kind, string Path, long Bytes, string Note);
