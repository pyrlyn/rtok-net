namespace Rtok.Store;

/// <summary>Measured savings of one plugin on one UTC day.</summary>
/// <param name="Day">Days since the Unix epoch, UTC.</param>
public sealed record MeasurementDay(
    long Day,
    string Plugin,
    long Rows,
    long EstBefore,
    long EstAfter
);
