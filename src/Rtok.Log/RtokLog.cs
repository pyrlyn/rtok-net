using Microsoft.Extensions.Logging;
using Pyrlyn.RotatingLog;
using Rtok.Config;
using Rtok.Store;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Rtok.Log;

/// <summary>
/// rtok's one log pipeline (N20): the rotating file, the <c>logs</c> rows with <c>to_db</c>, and
/// stderr under <c>RTOK_LOG</c>, built in code from <c>[log]</c>. Every sink fails open.
/// </summary>
public static class RtokLog
{
    /// <summary>The variable that turns on the stderr log.</summary>
    public const string DebugVariable = "RTOK_LOG";

    // Categories of libraries that log through ILogger. Their information (EF's every SQL
    // command) is noise in rtok's log unless someone asked for debug.
    private static readonly string[] Libraries = ["Microsoft", "System", "ModelContextProtocol"];

    /// <summary>
    /// The logger for <paramref name="log"/>. Its minimum level is the <c>[log] level</c> floor
    /// (lower only while <c>RTOK_LOG</c> asks for more on stderr), so an event below it reaches no
    /// sink. Dispose it at exit: that closes the store the rows went to.
    /// </summary>
    /// <param name="log">The <c>[log]</c> section, its path already expanded.</param>
    /// <param name="options">The store, the <c>RTOK_LOG</c> value and the clock.</param>
    /// <returns>The logger.</returns>
    public static Logger Create(LogSection log, RtokLogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        options ??= new RtokLogOptions();
        var floor = LogLevels.Floor(log.Level);
        var debug = LogLevels.Debug(
            options.Debug ?? Environment.GetEnvironmentVariable(DebugVariable)
        );
        var minimum = debug is { } d && d < floor ? d : floor;
        var libraries =
            minimum <= LogEventLevel.Debug ? minimum
            : minimum < LogEventLevel.Warning ? LogEventLevel.Warning
            : minimum;

        var config = new LoggerConfiguration().MinimumLevel.Is(minimum);
        foreach (var prefix in Libraries)
        {
            config.MinimumLevel.Override(prefix, libraries);
        }
        if (File(log, options.Time) is { } file)
        {
            config.WriteTo.Sink(new RotatingFileSink(file), floor);
        }
        if (log.ToDb && options.OpenStore is { } open)
        {
            config.WriteTo.Sink(new StoreLogSink(open), floor);
        }
        if (debug is { } level)
        {
            if (options.DebugWriter is { } writer)
            {
                config.WriteTo.Sink(new WriterSink(writer), level);
            }
            else
            {
                config.WriteTo.Console(
                    new LineFormatter(),
                    level,
                    standardErrorFromLevel: LogEventLevel.Verbose
                );
            }
        }
        return config.CreateLogger();
    }

    /// <summary>
    /// The <c>ILoggerFactory</c> that EF Core, ASP.NET Core and the MCP SDK log through: the
    /// same pipeline, the category as the line's source and the event id's name as its name.
    /// </summary>
    /// <param name="logger">The pipeline; the factory does not dispose it.</param>
    /// <returns>The factory.</returns>
    public static ILoggerFactory LoggerFactory(Serilog.ILogger logger) =>
        new SerilogLoggerFactory(logger, dispose: false);

    // A path that cannot be a file (empty, invalid characters) leaves the log without its file
    // rather than the command without a logger.
    private static RotatingFileLog? File(LogSection log, TimeProvider time)
    {
        try
        {
            return new RotatingFileLog(
                new RotatingFileLogOptions
                {
                    Path = log.Path,
                    MaxBytes = (long)Math.Min(log.MaxBytes, long.MaxValue),
                    Files = (int)Math.Min(log.Files, int.MaxValue),
                    Level = log.Level,
                    Time = time,
                }
            );
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The stderr lines into a writer of the caller's, for tests.</summary>
    private sealed class WriterSink(TextWriter writer) : ILogEventSink
    {
        private readonly LineFormatter _format = new();
        private readonly Lock _gate = new();

        public void Emit(LogEvent logEvent)
        {
            lock (_gate)
            {
                _format.Format(logEvent, writer);
            }
        }
    }
}
