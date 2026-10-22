namespace Rtok.Agents.Junk;

/// <summary>One folder a host writes, as a template.</summary>
/// <param name="Role">What it holds.</param>
/// <param name="Path">A template under a <see cref="JunkRoots"/> token.</param>
/// <param name="Documented">A row of research.md §22, not a guess: only these may be cleared.</param>
/// <param name="PerCommit">
/// One subfolder per host build (VS Code's <c>CachedData/&lt;commit&gt;</c>): only the builds that
/// are no longer current are cache, never the folder itself.
/// </param>
public sealed record JunkSpec(JunkRole Role, string Path, bool Documented, bool PerCommit = false);
