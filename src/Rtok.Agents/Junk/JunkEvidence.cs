namespace Rtok.Agents.Junk;

/// <summary>The evidence strings D36 accepts, and the reasons shared across kinds.</summary>
public static class JunkEvidence
{
    /// <summary>A row of research.md §22.</summary>
    public const string Section22 = "research.md §22";

    /// <summary>A valid <c>CACHEDIR.TAG</c>.</summary>
    public const string Tag = "CACHEDIR.TAG";

    /// <summary>rtok wrote it.</summary>
    public const string RtokOwn = "rtok-owned";

    /// <summary><c>rtok worktree gc</c> would remove it.</summary>
    public const string GcVerdict = "gc verdict";

    /// <summary>The user named it in <c>[agents.junk] extra</c>.</summary>
    public const string Extra = "[agents.junk] extra";

    /// <summary>The evidence column of a find nothing documents.</summary>
    public const string None = "none";

    /// <summary>Why a find without D36 evidence stays.</summary>
    public const string NotDocumented =
        "not documented: not cleared (add to [agents.junk] extra to clear)";

    /// <summary>A walk that hit its deadline: its newest mtime is a lower bound, so the item is not idle.</summary>
    public const string ScanStopped = "scan stopped: not cleared";

    /// <summary>Why an item stays when its newest mtime could not be established.</summary>
    public const string AgeUnknown = "age unknown: a file or its mtime could not be read";

    /// <summary>Whether <paramref name="evidence"/> lets <c>clear</c> plan an item.</summary>
    public static bool Documented(string evidence) =>
        evidence is Section22 or Tag or RtokOwn or Extra or GcVerdict;
}
