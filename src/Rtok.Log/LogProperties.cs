using Serilog;

namespace Rtok.Log;

/// <summary>
/// The event properties rtok's own code sets. A line is <c>&lt;source&gt;/&lt;name&gt;: message</c>;
/// an event with <see cref="Source"/> is rtok's own and also becomes a <c>logs</c> row.
/// </summary>
public static class LogProperties
{
    /// <summary>What wrote the event: a surface, a subsystem or a plugin id.</summary>
    public const string Source = "Source";

    /// <summary>The event name.</summary>
    public const string Name = "Name";

    /// <summary>The agent session the event belongs to, for the <c>logs</c> row.</summary>
    public const string Session = "SessionId";

    /// <summary>The <c>calls</c> row the event belongs to, for the <c>logs</c> row.</summary>
    public const string CallId = "CallId";

    /// <summary>The plugin that logged, for the <c>logs</c> row.</summary>
    public const string Plugin = "Plugin";

    /// <summary>A logger whose events come from <paramref name="source"/>.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="source">What writes through it.</param>
    /// <returns>The logger with the source attached.</returns>
    public static ILogger ForSource(this ILogger logger, string source)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.ForContext(Source, source);
    }

    /// <summary>A logger whose next event is called <paramref name="name"/>.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="name">The event name.</param>
    /// <returns>The logger with the name attached.</returns>
    public static ILogger Named(this ILogger logger, string name)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.ForContext(Name, name);
    }
}
