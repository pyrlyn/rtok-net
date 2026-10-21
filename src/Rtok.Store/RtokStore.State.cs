using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Stores one measurement; see <see cref="InsertMeasurements"/>.</summary>
    public void InsertMeasurement(
        string session,
        MeasurementInput measurement,
        string? once = null
    ) => InsertMeasurements(session, [measurement], once);

    /// <summary>Stores <paramref name="measurements"/> for one delivery of a call, all or none.
    /// With <paramref name="once"/> (the call, e.g. <c>PreToolUse:&lt;tool_use_id&gt;</c>) a row
    /// whose once key (once, plugin, kind and ref) is already stored is dropped, so a call
    /// delivered twice counts once.</summary>
    public void InsertMeasurements(
        string session,
        IReadOnlyList<MeasurementInput> measurements,
        string? once = null
    )
    {
        ArgumentNullException.ThrowIfNull(measurements);
        using var scope = EnterWrite();
        var db = scope.Db;
        foreach (var m in measurements)
        {
            var onceKey = once is null ? null : $"{once}|{m.Plugin}|{m.Kind}|{m.RefId}";
            if (onceKey is not null && db.Measurements.Any(x => x.OnceKey == onceKey))
            {
                continue;
            }
            db.Measurements.Add(
                new Measurement
                {
                    Ts = Now,
                    Session = session,
                    Plugin = m.Plugin,
                    Kind = m.Kind,
                    BeforeBytes = m.BeforeBytes,
                    AfterBytes = m.AfterBytes,
                    EstBefore = m.EstBefore,
                    EstAfter = m.EstAfter,
                    RefId = m.RefId,
                    CallId = m.CallId,
                    OnceKey = onceKey,
                }
            );
            // Saved one by one: the next row's once check must see this one.
            db.SaveChanges();
        }
        scope.Commit();
    }

    public string? KvGet(string key)
    {
        using var scope = Enter();
        var db = scope.Db;
        var k = key;
        return db.Kv.Where(e => e.Key == k).Select(e => e.Value).FirstOrDefault();
    }

    public void KvSet(string key, string value)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var k = key;
        var row = db.Kv.FirstOrDefault(e => e.Key == k);
        if (row is null)
        {
            db.Kv.Add(new KvEntry { Key = key, Value = value });
        }
        else
        {
            row.Value = value;
        }
        db.SaveChanges();
        scope.Commit();
    }

    public void KvDelete(string key)
    {
        using var scope = Enter();
        var db = scope.Db;
        var k = key;
        db.Kv.Where(e => e.Key == k).ExecuteDelete();
    }

    /// <summary>A plugin's own state value (<c>plugin_state</c>), or null.</summary>
    public string? PluginStateGet(string plugin, string key)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (p, k) = (plugin, key);
        return db
            .PluginState.Where(s => s.Plugin == p && s.Key == k)
            .Select(s => s.Value)
            .FirstOrDefault();
    }

    public void PluginStateSet(string plugin, string key, string value)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var (p, k) = (plugin, key);
        var row = db.PluginState.FirstOrDefault(s => s.Plugin == p && s.Key == k);
        if (row is null)
        {
            db.PluginState.Add(
                new PluginState
                {
                    Plugin = plugin,
                    Key = key,
                    Value = value,
                }
            );
        }
        else
        {
            row.Value = value;
        }
        db.SaveChanges();
        scope.Commit();
    }
}
