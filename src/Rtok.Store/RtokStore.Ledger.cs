using Microsoft.EntityFrameworkCore;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>The saved stdin of the last <paramref name="limit"/> hook calls in
    /// <paramref name="session"/>, newest first, with the session fields spliced back in. A body
    /// saved only by size is left out.</summary>
    public IReadOnlyList<string> RecentHookInputs(string session, int limit)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (id, surface, take) = (session, "hook", Math.Max(0, limit));
        var rows = db
            .Calls.Where(c => c.SessionId == id && c.Surface == surface)
            .Join(db.CallIo, c => c.Id, io => io.CallId, (c, io) => io)
            .Where(io => io.RequestJson != null)
            .OrderByDescending(io => io.CallId)
            .Take(take)
            .ToList();
        var keys = rows.Where(r => r.HookSessionId is not null)
            .Select(r => r.HookSessionId!.Value)
            .Distinct()
            .ToList();
        var fields = db
            .HookSessions.Where(h => keys.Contains(h.Id))
            .ToList()
            .ToDictionary(h => h.Id, h => h.Fields);
        return
        [
            .. rows.Select(r =>
                HookFields.Rebuild(
                    r.RequestJson!,
                    r.HookSessionId is { } key ? fields.GetValueOrDefault(key) : null
                )
            ),
        ];
    }

    /// <summary>How many calls <paramref name="session"/> has made at or after the unix time
    /// <paramref name="ts"/>.</summary>
    public long CallsSince(string session, long ts)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (id, since) = (session, ts);
        return db.Calls.LongCount(c => c.SessionId == id && c.Ts >= since);
    }

    /// <summary>The newest <c>ref_id</c> of a <paramref name="plugin"/>/<paramref name="kind"/>
    /// measurement in <paramref name="session"/>.</summary>
    public string? LastMeasurementRef(string session, string plugin, string kind)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (id, owner, what) = (session, plugin, kind);
        return db
            .Measurements.Where(m => m.Session == id && m.Plugin == owner && m.Kind == what)
            .OrderByDescending(m => m.Id)
            .Select(m => m.RefId)
            .FirstOrDefault();
    }
}
