using Pyrlyn.RotatingLog;
using Serilog.Core;
using Serilog.Events;

namespace Rtok.Log;

/// <summary>
/// The file sink: every event as one line in the rotating log, which keeps the <c>.N</c> files,
/// the <c>errors.log</c> copy and the cross-process lock. It never throws.
/// </summary>
internal sealed class RotatingFileSink(RotatingFileLog log) : ILogEventSink
{
    public void Emit(LogEvent logEvent) =>
        log.Write(
            LogLevels.Name(logEvent.Level),
            LogEventText.Source(logEvent),
            LogEventText.Name(logEvent),
            LogEventText.Message(logEvent)
        );
}
