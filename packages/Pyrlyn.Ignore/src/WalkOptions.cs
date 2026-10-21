namespace Pyrlyn.Ignore;

/// <summary>What a <see cref="RepoWalker"/> honours. The defaults walk a repo the way git sees it.</summary>
public sealed record WalkOptions
{
    /// <summary>The defaults.</summary>
    public static WalkOptions Default { get; } = new();

    /// <summary><c>.gitignore</c> at every level.</summary>
    public bool GitIgnore { get; init; } = true;

    /// <summary><c>.ignore</c> at every level, git or not; it beats <c>.gitignore</c>.</summary>
    public bool DotIgnore { get; init; } = true;

    /// <summary>The repository's <c>info/exclude</c>.</summary>
    public bool GitExclude { get; init; } = true;

    /// <summary>The global excludes file (<c>core.excludesFile</c>, else <c>$XDG_CONFIG_HOME/git/ignore</c>).</summary>
    public bool GlobalExcludes { get; init; } = true;

    /// <summary>Use this file as the global excludes file instead of looking it up in git's config.</summary>
    public string? GlobalExcludesFile { get; init; }

    /// <summary>Honour the git sources only inside a git repository, as git does.</summary>
    public bool RequireGit { get; init; } = true;

    /// <summary>Read the ignore files of the directories between the repository root and the walk root.</summary>
    public bool Parents { get; init; } = true;

    /// <summary>Walk dotfiles and dot-directories; <c>.git</c> is never walked.</summary>
    public bool Hidden { get; init; } = true;

    /// <summary>Skip a linked git worktree below the root (its <c>.git</c> is a file naming a main checkout); submodules and nested clones stay.</summary>
    public bool SkipLinkedWorktrees { get; init; } = true;

    /// <summary>Match patterns without regard to case (git's <c>core.ignoreCase</c>).</summary>
    public bool IgnoreCase { get; init; }

    /// <summary>The deepest entry to yield; the root's children are depth 1. <see langword="null"/> is unlimited.</summary>
    public int? MaxDepth { get; init; }

    /// <summary>
    /// Gitignore-style patterns, relative to the root, that are walked even when an ignore file
    /// names them: additive, with the ignore files off below the subtree they start in.
    /// </summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>Gitignore-style patterns, relative to the root, never walked; they beat <see cref="Include"/>.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>The home directory: refused as a root, and where git's global config lives. Defaults to the user profile.</summary>
    public string? Home { get; init; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary><c>$XDG_CONFIG_HOME</c>; empty or <see langword="null"/> means <c>~/.config</c>.</summary>
    public string? XdgConfigHome { get; init; } =
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
}
