using Rtok.Config;

namespace Rtok.Agents.Junk;

/// <summary>What one report covers and the rules it judges by.</summary>
public sealed record JunkOptions
{
    /// <summary>The loaded config.</summary>
    public required RtokConfig Config { get; init; }

    /// <summary>rtok's home, for <c>~/.rtok</c> in extra and exclude paths.</summary>
    public required string RtokHome { get; init; }

    /// <summary>The template roots.</summary>
    public required JunkRoots Roots { get; init; }

    /// <summary>The hosts, in report order.</summary>
    public required IReadOnlyList<JunkHost> Hosts { get; init; }

    /// <summary>The run's clock, cwd, idle window and walk limit.</summary>
    public required JunkContext Context { get; init; }

    /// <summary>Also hosts that are not installed.</summary>
    public bool All { get; init; }

    /// <summary>The store; null when it cannot be opened (no archives then).</summary>
    public IJunkStore? Store { get; init; }

    /// <summary>The agent worktrees <c>build</c> looks in, as (host, path); empty until the worktree commands list them.</summary>
    public IReadOnlyList<(string Host, string Path)> Worktrees { get; init; } = [];

    /// <summary>The <c>stale-worktrees</c> items with the agent row each belongs to; empty until gc judges them.</summary>
    public IReadOnlyList<(string Agent, JunkItem Item)> StaleWorktrees { get; init; } = [];

    /// <summary>The language-server state roots rtok confines to each project; empty until the graph plugin keeps them.</summary>
    public IReadOnlyList<string> LspStates { get; init; } = [];
}
