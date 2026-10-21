namespace Rtok.Store;

/// <summary>One <c>measurements</c> row as a plugin reports it: no row, no saving claim.</summary>
/// <param name="Plugin">Catalog id of the plugin that made the change.</param>
/// <param name="Kind">Plugin-defined subtype, e.g. <c>formatter</c>, <c>inject</c>, <c>dedup</c>.</param>
/// <param name="RefId">Archive id or other handle that makes the saving reversible.</param>
public sealed record MeasurementInput(
    string Plugin,
    string Kind,
    long BeforeBytes,
    long AfterBytes,
    long EstBefore,
    long EstAfter,
    string? RefId = null,
    long? CallId = null
);
