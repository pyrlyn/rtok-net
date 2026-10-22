namespace Rtok.Agents.Junk;

/// <summary>What the rules need to know about one run.</summary>
/// <param name="Cwd">Where the command runs, canonical: a tagged cache of that worktree stays.</param>
/// <param name="Idle">A tagged or rtok cache modified within this window stays.</param>
/// <param name="Now">The clock of this run.</param>
/// <param name="Limit">How long one walk may take; a longer one is a lower bound, not a hang.</param>
public sealed record JunkContext(string Cwd, TimeSpan Idle, DateTime Now, TimeSpan Limit)
{
    /// <summary>T152's default idle window.</summary>
    public static readonly TimeSpan DefaultIdle = TimeSpan.FromHours(24);

    /// <summary>How long one agent's folders may take to size.</summary>
    public static readonly TimeSpan ScanLimit = TimeSpan.FromSeconds(10);
}
