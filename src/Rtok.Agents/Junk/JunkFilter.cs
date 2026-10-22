namespace Rtok.Agents.Junk;

/// <summary>The <c>clear</c> flags that choose what goes.</summary>
public sealed record JunkFilter
{
    /// <summary>What <c>--kind</c> takes: rtok's own junk, then the agent kinds. <c>snapshots</c> never clears anything.</summary>
    public static IReadOnlyList<string> KindNames { get; } = ["log", "archive", .. JunkScan.Kinds];

    /// <summary><c>--agent</c>: rtok or host ids; empty takes every agent.</summary>
    public IReadOnlyList<string> Agents { get; init; } = [];

    /// <summary><c>--kind</c>; empty takes every kind the class gate lets through.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];

    /// <summary><c>--include review</c>.</summary>
    public bool IncludeReview { get; init; }

    /// <summary><c>--older-than</c>: only items whose newest file is at least this old.</summary>
    public TimeSpan? OlderThan { get; init; }

    /// <summary><c>--trash</c>: move to the OS trash instead of removing.</summary>
    public bool Trash { get; init; }

    /// <summary>Only items at least this big: what the dry run shows is what <c>--yes</c> removes.</summary>
    public long MinSize { get; init; }

    /// <summary>No selecting flag, or <c>--agent rtok</c> alone: rtok's own logs and archives only, so a new deletion always needs a flag.</summary>
    public bool OwnOnly =>
        Kinds.Count == 0
        && !IncludeReview
        && OlderThan is null
        && MinSize == 0
        && Agents.All(a => a is "rtok");

    internal bool WantsAgent(string name) =>
        Agents.Count == 0 || Agents.Contains(name, StringComparer.Ordinal);

    /// <summary>The class gate: safe by default, review when named or with <c>--include review</c>, explicit only when named, never never.</summary>
    internal bool WantsKind(string kind, string cls)
    {
        var named = Kinds.Contains(kind, StringComparer.Ordinal);
        if (Kinds.Count > 0 && !named)
        {
            return false;
        }
        return cls switch
        {
            "safe" => true,
            "review" => named || IncludeReview,
            "explicit" => named,
            _ => false,
        };
    }

    /// <summary>Why <c>--agent</c>'s value is refused, or null: rtok or one of <paramref name="hosts"/>.</summary>
    public static string? AgentError(string value, IEnumerable<string> hosts) =>
        value is "rtok" || hosts.Contains(value, StringComparer.Ordinal)
            ? null
            : $"not an agent: {value} (rtok or a host id; see `rtok agents list`)";

    /// <summary>Why <c>--kind</c>'s value is refused, or null.</summary>
    public static string? KindError(string value) =>
        KindNames.Contains(value, StringComparer.Ordinal)
            ? null
            : $"not a junk kind: {value} (one of {string.Join(", ", KindNames)})";
}
