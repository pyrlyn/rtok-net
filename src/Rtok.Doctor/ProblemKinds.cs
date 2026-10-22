namespace Rtok.Doctor;

/// <summary>The kinds a <see cref="Problem"/> has, as <c>--json</c> spells them.</summary>
public static class ProblemKinds
{
    /// <summary>A hook whose program or script is not there.</summary>
    public const string BrokenHook = "broken-hook";

    /// <summary>A hook that runs, but likely not as meant: a script without its exec bit.</summary>
    public const string SuspectHook = "suspect-hook";

    /// <summary>A hook that cannot be judged without running it.</summary>
    public const string UnverifiedHook = "unverified-hook";

    /// <summary>A hook that runs more than once per event.</summary>
    public const string DuplicateHook = "duplicate-hook";

    /// <summary>An MCP server defined twice, or launched twice under two names.</summary>
    public const string DuplicateMcp = "duplicate-mcp";

    /// <summary>One package at two versions: not duplicates, but worth a look.</summary>
    public const string ConflictingMcp = "conflicting-mcp";

    /// <summary>rtok's own entry seen more than once: information only.</summary>
    public const string OwnMcp = "own-mcp";

    /// <summary>An enabled plugin whose install directory is gone.</summary>
    public const string StalePlugin = "stale-plugin";

    /// <summary>A host config that cannot be read or parsed.</summary>
    public const string UnreadableConfig = "unreadable-config";

    /// <summary>What <c>--only</c> names, mapped to the kind it fixes.</summary>
    public static IReadOnlyDictionary<string, string> Only { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["broken-hooks"] = BrokenHook,
            ["duplicate-hooks"] = DuplicateHook,
            ["duplicate-mcp"] = DuplicateMcp,
        };

    /// <summary>The kinds <c>--fix</c> removes, in <c>--only</c> order.</summary>
    public static IReadOnlyList<string> Fixed { get; } = [BrokenHook, DuplicateHook, DuplicateMcp];
}
