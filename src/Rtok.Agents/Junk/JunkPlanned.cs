namespace Rtok.Agents.Junk;

/// <summary>One item of the <c>clear</c> plan and what happened to it; the <c>--json</c> item shape.</summary>
public sealed class JunkPlanned
{
    /// <summary>The agent row it came from.</summary>
    public required string Agent { get; init; }

    /// <summary>The kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The path.</summary>
    public required string Path { get; init; }

    /// <summary>Its bytes.</summary>
    public required long Bytes { get; init; }

    /// <summary>Files in it, counted for a planned item before anything is removed.</summary>
    public long Files { get; set; }

    /// <summary>The newest file mtime; null when unreadable.</summary>
    public DateTime? LastUsed { get; init; }

    /// <summary>Why it is junk, as <c>list</c> says it.</summary>
    public required string Reason { get; init; }

    /// <summary>In the plan; false for an item left at planning time (a running agent's).</summary>
    public bool Planned { get; set; }

    /// <summary><c>clear</c> (removed, or would be on a dry run) or <c>skip</c>.</summary>
    public string Action { get; set; } = "clear";

    /// <summary>What happened, or why it stays.</summary>
    public string Note { get; set; } = "";

    /// <summary>Planned and not removed: the re-check refused it or the removal failed.</summary>
    public bool Failed { get; set; }

    internal string Evidence { get; init; } = "";

    /// <summary>The real path at planning time: another one at removal means a link was swapped in.</summary>
    internal string Real { get; init; } = "";

    internal bool Clears => Action is "clear";

    internal void Skip(string why, bool failed)
    {
        Action = "skip";
        Note = why;
        Failed = failed;
    }
}
