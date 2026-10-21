namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>The newest calls first, at most <paramref name="limit"/>.</summary>
    public IReadOnlyList<CallSummary> RecentCalls(int limit)
    {
        using var scope = Enter();
        var db = scope.Db;
        var n = limit;
        return db
            .Calls.OrderByDescending(c => c.Id)
            .Take(n)
            .Select(c => new CallSummary(
                c.Id,
                c.Ts,
                c.SessionId,
                c.Surface,
                c.Kind,
                c.Plugin,
                c.Name,
                c.ParentId,
                c.Ms,
                c.Ok,
                c.Error,
                db.Hosts.Where(h => h.Id == c.HostId).Select(h => h.Slug).FirstOrDefault(),
                db.Providers.Where(p => p.Id == c.ProviderId).Select(p => p.Slug).FirstOrDefault(),
                db.Models.Where(m => m.Id == c.ModelId).Select(m => m.Slug).FirstOrDefault(),
                db.Usage.Where(u => u.CallId == c.Id).Sum(u => (long?)u.Input),
                db.Usage.Where(u => u.CallId == c.Id).Sum(u => (long?)u.CacheCreate),
                db.Usage.Where(u => u.CallId == c.Id).Sum(u => (long?)u.CacheRead),
                db.Usage.Where(u => u.CallId == c.Id).Sum(u => (long?)u.Output)
            ))
            .ToList();
    }

    /// <summary>The sessions most recently active first, at most <paramref name="limit"/>.</summary>
    public IReadOnlyList<SessionSummary> RecentSessions(int limit)
    {
        using var scope = Enter();
        var db = scope.Db;
        var n = limit;
        return db
            .Sessions.Select(s => new
            {
                Session = s,
                LastCall = db.Calls.Where(c => c.SessionId == s.Id).Max(c => (long?)c.Ts),
            })
            .OrderByDescending(x => x.LastCall != null ? x.LastCall.Value : x.Session.StartedAt)
            .ThenBy(x => x.Session.Id)
            .Take(n)
            .Select(x => new SessionSummary(
                x.Session.Id,
                db.Hosts.Where(h => h.Id == x.Session.HostId).Select(h => h.Slug).FirstOrDefault(),
                x.Session.Project,
                x.Session.Cwd,
                x.Session.SourceName,
                x.Session.StartedAt,
                x.Session.EndedAt,
                x.LastCall != null ? x.LastCall.Value : x.Session.StartedAt,
                db.Usage.Where(u => u.Session == x.Session.Id).Sum(u => u.Input),
                db.Usage.Where(u => u.Session == x.Session.Id).Sum(u => u.CacheCreate),
                db.Usage.Where(u => u.Session == x.Session.Id).Sum(u => u.CacheRead),
                db.Usage.Where(u => u.Session == x.Session.Id).Sum(u => u.Output)
            ))
            .ToList();
    }

    /// <summary>Every <c>usage</c> row summed.</summary>
    public UsageTotals TotalUsage()
    {
        using var scope = Enter();
        var db = scope.Db;
        return new UsageTotals(
            db.Usage.Sum(u => u.Input),
            db.Usage.Sum(u => u.CacheCreate),
            db.Usage.Sum(u => u.CacheRead),
            db.Usage.Sum(u => u.Output),
            db.Usage.LongCount()
        );
    }

    /// <summary>The context of the last <paramref name="limit"/> turns, oldest first: each
    /// <c>usage</c> row's input plus its cache writes and reads.</summary>
    public IReadOnlyList<long> RecentTurns(int limit)
    {
        using var scope = Enter();
        var db = scope.Db;
        var n = limit;
        var newest = db
            .Usage.OrderByDescending(u => u.Id)
            .Take(n)
            .Select(u => u.Input + u.CacheCreate + u.CacheRead)
            .ToList();
        newest.Reverse();
        return newest;
    }

    /// <summary>Measurement rows at or after <paramref name="since"/> (Unix seconds), summed per
    /// UTC day and plugin, oldest day first.</summary>
    public IReadOnlyList<MeasurementDay> MeasurementDays(long since)
    {
        using var scope = Enter();
        var db = scope.Db;
        var from = since;
        // Anonymous rows of exact types: the query precompiler models no implicit conversions.
        var rows = db
            .Measurements.Where(m => m.Ts >= from)
            .GroupBy(m => new { Day = m.Ts / 86400L, m.Plugin })
            .OrderBy(g => g.Key.Day)
            .ThenBy(g => g.Key.Plugin)
            .Select(g => new
            {
                g.Key.Day,
                g.Key.Plugin,
                Rows = g.LongCount(),
                EstBefore = g.Sum(m => m.EstBefore),
                EstAfter = g.Sum(m => m.EstAfter),
            })
            .ToList();
        return
        [
            .. rows.Select(r => new MeasurementDay(
                r.Day,
                r.Plugin,
                r.Rows,
                r.EstBefore,
                r.EstAfter
            )),
        ];
    }
}
