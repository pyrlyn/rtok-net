namespace Rtok.Model;

/// <summary>Measured savings on one UTC day.</summary>
/// <param name="Day">The day, <c>yyyy-MM-dd</c>.</param>
/// <param name="Rows">Measurement rows that day.</param>
/// <param name="Saved">Estimated tokens saved: before minus after.</param>
/// <param name="Plugins">Tokens saved per plugin id.</param>
public sealed record SavingsDay(
    string Day,
    long Rows,
    long Saved,
    IReadOnlyDictionary<string, long> Plugins
);
