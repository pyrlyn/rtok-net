namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>The <c>calls</c> rows at or after <paramref name="since"/> (Unix seconds), and
    /// all of them.</summary>
    public (long InWindow, long Total) CallCounts(long since)
    {
        using var scope = Enter();
        var db = scope.Db;
        var from = since;
        return (db.Calls.LongCount(c => c.Ts >= from), db.Calls.LongCount());
    }

    /// <summary>The <c>measurements</c> and <c>usage</c> rows at or after
    /// <paramref name="since"/>, and all of them.</summary>
    public (long Measurements, long MeasurementsTotal, long Usage, long UsageTotal) LedgerCounts(
        long since
    )
    {
        using var scope = Enter();
        var db = scope.Db;
        var from = since;
        return (
            db.Measurements.LongCount(m => m.Ts >= from),
            db.Measurements.LongCount(),
            db.Usage.LongCount(u => u.Ts >= from),
            db.Usage.LongCount()
        );
    }

    /// <summary>Each hook, mcp and proxy call at or after <paramref name="since"/>: its surface,
    /// its latency when it recorded one, a hook's event name, and whether a
    /// <c>measurements</c> row points at it; oldest first.</summary>
    public IReadOnlyList<CallTiming> CallTimings(long since)
    {
        using var scope = Enter();
        var db = scope.Db;
        var from = since;
        // Two queries: the precompiler cannot translate a subquery over the projected call.
        var measured = db
            .Measurements.Where(m =>
                m.CallId != null && db.Calls.Any(c => c.Id == m.CallId && c.Ts >= from)
            )
            .Select(m => m.CallId)
            .AsEnumerable()
            .OfType<long>()
            .ToHashSet();
        return
        [
            .. db
                .Calls.Where(c =>
                    c.Ts >= from
                    && (c.Surface == "hook" || c.Surface == "mcp" || c.Surface == "proxy")
                )
                .OrderBy(c => c.Id)
                .AsEnumerable()
                .Select(c => new CallTiming(c.Surface, c.Name, c.Ms, measured.Contains(c.Id))),
        ];
    }

    /// <summary>Every <c>measurements</c> row at or after <paramref name="since"/> (Unix
    /// seconds), oldest first.</summary>
    public IReadOnlyList<MeasurementInput> MeasurementsSince(long since)
    {
        using var scope = Enter();
        var db = scope.Db;
        var from = since;
        return
        [
            .. db
                .Measurements.Where(m => m.Ts >= from)
                .OrderBy(m => m.Id)
                .AsEnumerable()
                .Select(Input),
        ];
    }

    private static MeasurementInput Input(Model.Measurement m) =>
        new(
            m.Plugin,
            m.Kind,
            m.BeforeBytes,
            m.AfterBytes,
            m.EstBefore,
            m.EstAfter,
            m.RefId,
            m.CallId
        );
}
