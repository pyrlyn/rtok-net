namespace Rtok.Plugin.Sdk;

/// <summary>
/// A before/after pair produced by one plugin action (rtok D3): the only evidence of a saving
/// the host accepts. Before and after are the payload the model would have seen; the estimates
/// are the host's token estimate of each.
/// </summary>
public sealed record Measurement
{
    /// <summary>Catalogue id of the plugin that made the change.</summary>
    public required string Plugin { get; init; }

    /// <summary>Plugin-defined subtype, e.g. <c>formatter</c>, <c>rule</c>, <c>raw</c>,
    /// <c>inject</c>, <c>dedup</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Payload size in bytes before the change.</summary>
    public required long BeforeBytes { get; init; }

    /// <summary>Payload size in bytes after the change.</summary>
    public required long AfterBytes { get; init; }

    /// <summary>Estimated tokens before the change.</summary>
    public required int EstBefore { get; init; }

    /// <summary>Estimated tokens after the change.</summary>
    public required int EstAfter { get; init; }

    /// <summary>Archive id (or another handle) that makes the saving reversible.</summary>
    public string? RefId { get; init; }

    /// <summary>The owning call row, when the plugin has one.</summary>
    public long? CallId { get; init; }
}
