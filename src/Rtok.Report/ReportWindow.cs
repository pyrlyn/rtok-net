namespace Rtok.Report;

/// <summary>The window a report covers.</summary>
/// <param name="From">The window's start.</param>
/// <param name="To">The window's end.</param>
/// <param name="Since">The window as given: 30d, 12h.</param>
/// <param name="Store">The store the rows came from.</param>
public sealed record ReportWindow(
    DateTimeOffset From,
    DateTimeOffset To,
    string Since,
    string Store
)
{
    /// <summary>The start as a UTC date.</summary>
    public string FromDate =>
        From.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The end as a UTC date.</summary>
    public string ToDate =>
        To.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
