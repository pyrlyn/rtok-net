namespace Rtok.Agents.Junk;

/// <summary>A directory that is cache without a <c>CACHEDIR.TAG</c>: a §22 row or something rtok writes.</summary>
/// <param name="Path">The directory.</param>
/// <param name="Evidence">Its evidence.</param>
/// <param name="IdleRule">Keep it while touched within the idle window: a tool may be using it now.</param>
public sealed record JunkOwned(string Path, string Evidence, bool IdleRule);
