using System.Globalization;
using Rtok.Store;

namespace Rtok.Model;

/// <summary>The Overview page: usage totals, recent turns and measured savings per day. A saving
/// shows only where a measurement row backs it.</summary>
/// <param name="store">The store.</param>
/// <param name="days">How many days of savings, today included.</param>
/// <param name="turns">How many recent turns.</param>
public sealed class OverviewSource(RtokStore store, int days = 14, int turns = 50)
    : IPageSource<OverviewPage>
{
    private const long Day = 86400;

    /// <inheritdoc />
    public OverviewPage Read(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var totals = store.TotalUsage();
        var today = context.Time.GetUtcNow().ToUnixTimeSeconds() / Day;
        var savings = store
            .MeasurementDays((today - days + 1) * Day)
            .GroupBy(d => d.Day)
            .Select(g => new SavingsDay(
                DateTimeOffset
                    .FromUnixTimeSeconds(g.Key * Day)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Sum(d => d.Rows),
                g.Sum(d => d.EstBefore - d.EstAfter),
                g.ToDictionary(d => d.Plugin, d => d.EstBefore - d.EstAfter, StringComparer.Ordinal)
            ));
        return new OverviewPage(
            totals.Input,
            totals.CacheCreate,
            totals.CacheRead,
            totals.Output,
            totals.Rows,
            store.RecentTurns(turns),
            [],
            [.. savings]
        );
    }
}
