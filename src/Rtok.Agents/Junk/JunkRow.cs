namespace Rtok.Agents.Junk;

/// <summary>One item of the breakdown; the <c>--json</c> item shape.</summary>
public sealed class JunkRow
{
    /// <summary>The agent row it belongs to.</summary>
    public required string Agent { get; init; }

    /// <summary>The kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The class; empty for a planned item.</summary>
    public required string Class { get; init; }

    /// <summary>The path.</summary>
    public required string Path { get; init; }

    /// <summary>Bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>The newest file mtime; null when unread or unreadable, which counts as recent.</summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>Why it is junk.</summary>
    public string Reason { get; set; } = "";

    /// <summary><c>clear</c> takes it with these flags.</summary>
    public required bool WillClear { get; init; }

    /// <summary>Why <c>clear</c> leaves it.</summary>
    public string? SkipReason { get; init; }

    /// <summary>What a <c>clear --yes</c> did to it, for the text.</summary>
    internal string Result { get; init; } = "";

    internal string Evidence { get; init; } = "";

    /// <summary>Part of what the kind line sums.</summary>
    internal bool Counted { get; init; } = true;

    internal bool Stamped { get; set; }

    internal static JunkRow OfItem(string agent, JunkItem i) =>
        new()
        {
            Agent = agent,
            Kind = i.Kind,
            Class = i.Class,
            Path = i.Path,
            SizeBytes = i.Bytes,
            SkipReason =
                i.Kept
                ?? i.Class switch
                {
                    "review" => "review kind: add --include review",
                    "explicit" => $"explicit kind: name it with --kind {i.Kind}",
                    _ => null,
                },
            WillClear = i.Kept is null && i.Class is not ("review" or "explicit"),
            Evidence = i.Evidence,
            Counted = i.Counted,
        };

    internal static JunkRow OfOwn(JunkOutcome o) =>
        new()
        {
            Agent = "rtok",
            Kind = o.Kind,
            Class = "safe",
            Path = o.Path,
            SizeBytes = o.Bytes,
            Reason = o.Note,
            WillClear = true,
            Evidence = JunkEvidence.RtokOwn,
        };

    /// <summary>The last-used time and the reason, read only for a row about to be shown: the walk costs as much as sizing it.</summary>
    internal void Stamp(JunkView view)
    {
        if (Stamped)
        {
            return;
        }
        Stamped = true;
        LastUsed = JunkTree.Newest(Path).Newest;
        if (Reason.Length == 0)
        {
            Reason = view.Reason(Kind, Evidence, LastUsed);
        }
    }
}
