using System.Globalization;
using Pyrlyn.RotatingLog;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Rtok.Log;

/// <summary>
/// The one reading of an event every sink shares: its level name, source, name and message, and
/// the line they make (<see cref="RotatingFileLog.FormatLine"/>).
/// </summary>
internal static class LogEventText
{
    // `l`: string values plain, not quoted; `j`: structured values as JSON.
    private static readonly MessageTemplateTextFormatter Rendered = new(
        "{Message:lj}",
        CultureInfo.InvariantCulture
    );

    /// <summary><see cref="LogProperties.Source"/>, else the <c>ILogger</c> category, else <c>rtok</c>.</summary>
    public static string Source(LogEvent e) =>
        Text(e, LogProperties.Source) ?? Text(e, "SourceContext") ?? "rtok";

    /// <summary><see cref="LogProperties.Name"/>, else the <c>ILogger</c> event name, else <c>log</c>.</summary>
    public static string Name(LogEvent e) =>
        Text(e, LogProperties.Name)
        ?? (
            e.Properties.TryGetValue("EventId", out var id) && id is StructureValue s
                ? s
                    .Properties.FirstOrDefault(p =>
                        string.Equals(p.Name, "Name", StringComparison.Ordinal)
                    )
                    ?.Value
                    is ScalarValue { Value: string name }
                    ? name
                    : null
                : null
        )
        ?? "log";

    /// <summary>The rendered message, with the exception's type and message after it.</summary>
    public static string Message(LogEvent e)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        Rendered.Format(e, text);
        if (e.Exception is { } ex)
        {
            text.Write(": ");
            text.Write(ex.GetType().Name);
            text.Write(": ");
            text.Write(ex.Message);
        }
        return text.ToString();
    }

    /// <summary>The whole line, as the file holds it.</summary>
    public static string Line(LogEvent e) =>
        RotatingFileLog.FormatLine(
            e.Timestamp,
            LogLevels.Name(e.Level),
            Source(e),
            Name(e),
            Message(e)
        );

    /// <summary>A scalar property as text, or null when absent.</summary>
    public static string? Text(LogEvent e, string property) =>
        e.Properties.TryGetValue(property, out var value) && value is ScalarValue { Value: { } v }
            ? Convert.ToString(v, CultureInfo.InvariantCulture)
            : null;
}
