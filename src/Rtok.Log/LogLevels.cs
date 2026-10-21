using Serilog.Events;

namespace Rtok.Log;

/// <summary>rtok's four level names and Serilog's levels, both ways.</summary>
public static class LogLevels
{
    /// <summary>The name a line carries: <c>error</c>, <c>warn</c>, <c>info</c> or <c>debug</c>.</summary>
    /// <param name="level">The Serilog level.</param>
    /// <returns>The name; fatal is <c>error</c> and verbose is <c>debug</c>.</returns>
    public static string Name(LogEventLevel level) =>
        level switch
        {
            >= LogEventLevel.Error => "error",
            LogEventLevel.Warning => "warn",
            LogEventLevel.Information => "info",
            _ => "debug",
        };

    /// <summary>
    /// The Serilog level of a <c>[log] level</c> floor. An unknown name ranks as <c>error</c>, as
    /// it does in Pyrlyn.RotatingLog.
    /// </summary>
    /// <param name="name">The floor's name.</param>
    /// <returns>The lowest level that passes it.</returns>
    public static LogEventLevel Floor(string? name) =>
        name?.ToLowerInvariant() switch
        {
            "debug" => LogEventLevel.Debug,
            "info" => LogEventLevel.Information,
            "warn" => LogEventLevel.Warning,
            _ => LogEventLevel.Error,
        };

    /// <summary>
    /// The stderr level <c>RTOK_LOG</c> asks for: a level name (<c>trace</c> is verbose), or
    /// <c>debug</c> for any other value. Unset, empty, <c>off</c> and <c>0</c> mean no stderr log.
    /// </summary>
    /// <param name="value">The variable's value.</param>
    /// <returns>The level, or null for none.</returns>
    public static LogEventLevel? Debug(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "off" or "0" => null,
            "error" => LogEventLevel.Error,
            "warn" => LogEventLevel.Warning,
            "info" => LogEventLevel.Information,
            "trace" => LogEventLevel.Verbose,
            _ => LogEventLevel.Debug,
        };
}
