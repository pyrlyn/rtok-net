namespace Rtok.Plugin.Sdk;

/// <summary>One mechanical observation to store: a scrubbed tool-call summary, not the raw
/// output.</summary>
public sealed record NewObservation
{
    /// <summary>Host session that produced it.</summary>
    public required string SessionId { get; init; }

    /// <summary>Project key, when the session has one.</summary>
    public string? Project { get; init; }

    /// <summary><c>file_read</c>, <c>command_run</c>, <c>error</c>, ...</summary>
    public required string ObsType { get; init; }

    /// <summary>Tool name, capped.</summary>
    public required string Title { get; init; }

    /// <summary>Scrubbed narrative, capped.</summary>
    public required string Narrative { get; init; }

    /// <summary>SHA-256 of session, tool and narrative.</summary>
    public required string Dedup { get; init; }

    /// <summary>Existing root-relative files the call named.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}
