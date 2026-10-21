using System.Globalization;
using Rtok.Store;
using Serilog.Core;
using Serilog.Events;

namespace Rtok.Log;

/// <summary>
/// The <c>to_db</c> sink: each of rtok's own events (those with <see cref="LogProperties.Source"/>)
/// as a <c>logs</c> row. Library output stays in the file, so EF logging its own insert cannot
/// loop back here. The store opens on the first row; a store that will not open turns the sink
/// off, and a failed insert drops that row. Nothing reaches the caller.
/// </summary>
internal sealed class StoreLogSink(Func<RtokStore> open) : ILogEventSink, IDisposable
{
    private readonly Lock _gate = new();
    private RtokStore? _store;
    private bool _off;

    public void Emit(LogEvent logEvent)
    {
        if (LogEventText.Text(logEvent, LogProperties.Source) is not { } source)
        {
            return;
        }
        lock (_gate)
        {
            if (Store() is not { } store)
            {
                return;
            }
            try
            {
                store.InsertLog(
                    LogLevels.Name(logEvent.Level),
                    source,
                    LogEventText.Name(logEvent),
                    LogEventText.Message(logEvent),
                    LogEventText.Text(logEvent, LogProperties.Session),
                    long.TryParse(
                        LogEventText.Text(logEvent, LogProperties.CallId),
                        CultureInfo.InvariantCulture,
                        out var call
                    )
                        ? call
                        : null,
                    LogEventText.Text(logEvent, LogProperties.Plugin)
                );
            }
#pragma warning disable CA1031, RCS1075 // Fail open: a row the store refuses is not the caller's error.
            catch (Exception)
            {
                // The file line is already written; this row is dropped.
            }
#pragma warning restore CA1031, RCS1075
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _store?.Dispose();
            _store = null;
            _off = true;
        }
    }

    private RtokStore? Store()
    {
        if (_store is not null || _off)
        {
            return _store;
        }
        try
        {
            _store = open();
        }
#pragma warning disable CA1031 // Fail open: no store means no rows, never a failed command.
        catch (Exception)
#pragma warning restore CA1031
        {
            _off = true;
        }
        return _store;
    }
}
