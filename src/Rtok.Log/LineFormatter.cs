using Serilog.Events;
using Serilog.Formatting;

namespace Rtok.Log;

/// <summary>The stderr log in the file's own line shape, so a line reads the same in both.</summary>
internal sealed class LineFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        output.Write(LogEventText.Line(logEvent));
        output.Write('\n');
    }
}
