using System.Text.Json;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Store;

/// <summary>
/// The host plugins see on every surface, over the store (rtok's <c>Runtime</c>): savings go to
/// <c>measurements</c>, calls to <c>calls</c>, logs to <c>logs</c>, all attributed to one session.
/// The stored-state capabilities other tasks own answer as their stubs say
/// (<c>StoreRuntime.Pending.cs</c>); a throw there costs only the plugin that asked.
/// </summary>
public sealed partial class StoreRuntime(RtokStore store, StoreRuntimeOptions options)
    : ICapabilities
{
    private readonly EstimatorRates _rates = EstimatorRates.From(options.Config("estimator"));

    public string Session => options.Session;

    public string? Cwd => options.Cwd;

    /// <summary>The call this dispatch runs under: what <see cref="RecordCall"/> opened first,
    /// so plugin runs nest under it.</summary>
    public long? CallId { get; private set; }

    public int Estimate(string text, TextClass textClass) =>
        TokenEstimator.Estimate(text, textClass, _rates);

    public void Record(Measurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        store.InsertMeasurement(
            Session,
            new MeasurementInput(
                measurement.Plugin,
                measurement.Kind,
                measurement.BeforeBytes,
                measurement.AfterBytes,
                measurement.EstBefore,
                measurement.EstAfter,
                measurement.RefId,
                measurement.CallId
            ),
            options.Once
        );
    }

    public void PluginStateSet(string plugin, string key, string value) =>
        store.PluginStateSet(plugin, key, value);

    public string? PluginStateGet(string plugin, string key) => store.PluginStateGet(plugin, key);

    /// <summary>Opens a <c>calls</c> row, after the session's own row (rtok
    /// <c>record_call</c>). The first call becomes <see cref="CallId"/>; later ones nest under
    /// it.</summary>
    public long RecordCall(string surface, string kind, string? name)
    {
        if (CallId is null)
        {
            store.UpsertSession(Session, options.HostId, options.Project, Cwd, null);
        }
        var id = store.InsertCall(Session, surface, kind, options.HostId, name: name);
        if (CallId is { } parent)
        {
            store.SetCallParent(id, parent);
        }
        else
        {
            CallId = id;
        }
        return id;
    }

    public long RecordPluginRun(string surface, string plugin)
    {
        var id = store.InsertCall(Session, surface, "plugin_run", options.HostId, plugin: plugin);
        if (CallId is { } parent)
        {
            store.SetCallParent(id, parent);
        }
        return id;
    }

    public void RecordTokens(
        long callId,
        string? plugin,
        string phase,
        string source,
        long tokens
    ) => store.InsertTokens(callId, plugin, phase, source, tokens);

    public void Log(string level, string source, string name, string message)
    {
        try
        {
            store.InsertLog(level, source, name, message, Session, CallId);
        }
#pragma warning disable CA1031, RCS1075 // Never throws: a plugin cannot be made to care where logs go.
        catch (Exception) { }
#pragma warning restore CA1031, RCS1075
    }

    public JsonElement? ConfigJson(string path) => options.Config(path);

    public IReadOnlyList<string> RecentHookInputs(int limit) =>
        store.RecentHookInputs(Session, limit);

    public long CallsSince(long ts) => store.CallsSince(Session, ts);

    public string? LastMeasurementRef(string plugin, string kind) =>
        store.LastMeasurementRef(Session, plugin, kind);
}
