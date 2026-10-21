using Rtok.Log;

namespace Rtok.Model;

/// <summary>The Logs page: the <c>rtok logs</c> screen, the last <c>[log] lines</c> of rtok's
/// log numbered from the newest, without colour.</summary>
public sealed class LogsSource : IPageSource<TextPage>
{
    /// <inheritdoc />
    public TextPage Read(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var log = context.Config.Config.Log;
        var tail = LogTail.Read(log.Path, (int)Math.Min(log.Lines, int.MaxValue));
        var rows = tail.Count == 0 ? [LogScreen.Empty] : LogScreen.Numbered(tail, colour: false);
        return new TextPage(string.Concat(rows.Select(r => r + "\n")), Ready: true);
    }
}
