namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>
    /// The proxy's requests with <c>ts</c> in [<paramref name="since"/>, <paramref name="until"/>),
    /// ordered by api, session, model and second, each with its session's host. The caller cuts
    /// day and month boundaries in a time zone, so the grain here is the request.
    /// </summary>
    public IReadOnlyList<UsageRead> UsageSlices(long since, long until)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (from, to) = (since, until);
        var rows = db.Usage.Where(u => u.Ts >= from && u.Ts < to).ToList();
        var hosts = HostBySession(scope);
        return
        [
            .. rows.GroupBy(u => (u.Api, u.Session, u.Model, u.Ts))
                .Select(g => new UsageRead(
                    hosts.GetValueOrDefault(g.Key.Session),
                    g.Key.Api,
                    g.Key.Session,
                    g.Key.Model,
                    g.Key.Ts,
                    g.Sum(u => u.Input),
                    g.Sum(u => u.CacheCreate),
                    g.Sum(u => u.CacheRead),
                    g.Sum(u => u.Output)
                ))
                .OrderBy(r => r.Api, StringComparer.Ordinal)
                .ThenBy(r => r.Session, StringComparer.Ordinal)
                .ThenBy(r => r.Model, StringComparer.Ordinal)
                .ThenBy(r => r.Ts),
        ];
    }

    /// <summary>
    /// The tokens rtok saved per host with <c>ts</c> in [<paramref name="since"/>,
    /// <paramref name="until"/>): <c>est_before - est_after</c> over <c>measurements</c>. An
    /// <c>expand</c> row already reads negative, so the sum is the net saving. A null host is the
    /// measurements of sessions with no host row.
    /// </summary>
    public IReadOnlyList<(string? Host, long Saved)> MeasurementSavedByHost(long since, long until)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (from, to) = (since, until);
        var rows = db.Measurements.Where(m => m.Ts >= from && m.Ts < to).ToList();
        var hosts = HostBySession(scope);
        return
        [
            .. rows.GroupBy(m => hosts.GetValueOrDefault(m.Session), StringComparer.Ordinal)
                .Select(g => (g.Key, g.Sum(m => m.EstBefore - m.EstAfter)))
                .OrderBy(r => r.Key, StringComparer.Ordinal),
        ];
    }

    /// <summary>Every session's host slug; a session with no host row is left out.</summary>
    private static Dictionary<string, string> HostBySession(Scope scope)
    {
        var db = scope.Db;
        var sessions = db.Sessions.Where(s => s.HostId != null).ToList();
        var slugs = db.Hosts.ToList().ToDictionary(h => h.Id, h => h.Slug);
        var hosts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in sessions)
        {
            if (slugs.TryGetValue(s.HostId!.Value, out var slug))
            {
                hosts[s.Id] = slug;
            }
        }
        return hosts;
    }
}
