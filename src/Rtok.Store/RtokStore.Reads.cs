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
        // No anonymous types in these trees: the compiler builds them with an Expression.New
        // overload that trimming refuses. So the last activity is written out where it is used.
        return db
            .Sessions.OrderByDescending(s =>
                db.Calls.Where(c => c.SessionId == s.Id)
                    .Max(c => (long?)c.Ts)
                    .GetValueOrDefault(s.StartedAt)
            )
            .ThenBy(s => s.Id)
            .Take(n)
            .Select(s => new SessionSummary(
                s.Id,
                db.Hosts.Where(h => h.Id == s.HostId).Select(h => h.Slug).FirstOrDefault(),
                s.Project,
                s.Cwd,
                s.SourceName,
                s.StartedAt,
                s.EndedAt,
                db.Calls.Where(c => c.SessionId == s.Id)
                    .Max(c => (long?)c.Ts)
                    .GetValueOrDefault(s.StartedAt),
                db.Usage.Where(u => u.Session == s.Id).Sum(u => u.Input),
                db.Usage.Where(u => u.Session == s.Id).Sum(u => u.CacheCreate),
                db.Usage.Where(u => u.Session == s.Id).Sum(u => u.CacheRead),
                db.Usage.Where(u => u.Session == s.Id).Sum(u => u.Output)
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
        // A two-part group key would be an anonymous type (see RecentSessions), so each plugin
        // is grouped by day on its own; there are only as many as are linked.
        var plugins = db
            .Measurements.Where(m => m.Ts >= from)
            .Select(m => m.Plugin)
            .Distinct()
            .ToList();
        var days = new List<MeasurementDay>();
        foreach (var plugin in plugins)
        {
            var name = plugin;
            days.AddRange(
                db.Measurements.Where(m => m.Ts >= from && m.Plugin == name)
                    .GroupBy(m => m.Ts / 86400L)
                    .Select(g => new MeasurementDay(
                        g.Key,
                        name,
                        g.LongCount(),
                        g.Sum(m => m.EstBefore),
                        g.Sum(m => m.EstAfter)
                    ))
                    .ToList()
            );
        }
        return [.. days.OrderBy(d => d.Day).ThenBy(d => d.Plugin, StringComparer.Ordinal)];
    }
}
