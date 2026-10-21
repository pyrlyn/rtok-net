namespace Rtok.Log;

/// <summary><c>rtok logs watch</c>: the <c>rtok logs</c> screen, then the live file followed.</summary>
public static class LogWatch
{
    /// <summary>How often a watch looks.</summary>
    public static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The poll-and-print loop: every <paramref name="interval"/> <paramref name="step"/> says what
    /// changed, and null ends the watch. On a terminal the screen is redrawn in place (cursor up,
    /// clear below; no raw mode or alternate screen, so Ctrl-C leaves the terminal as it was).
    /// Piped, only the fresh rows are appended. A reader that went away (<c>| head</c>) ends it.
    /// </summary>
    /// <param name="output">Where rows go.</param>
    /// <param name="tty">Whether <paramref name="output"/> is a terminal.</param>
    /// <param name="interval">The pause between steps.</param>
    /// <param name="step">The next step, or null to stop.</param>
    /// <param name="cancel">Stops the watch.</param>
    public static void Loop(
        TextWriter output,
        bool tty,
        TimeSpan interval,
        Func<WatchTick?> step,
        CancellationToken cancel
    )
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(step);
        var shown = 0; // rows the terminal holds above the cursor
        try
        {
            while (!cancel.IsCancellationRequested && step() is { } tick)
            {
                if (tick.Fresh.Count > 0 || tick.Screen.Count != shown)
                {
                    if (tty)
                    {
                        Repaint(output, shown, tick.Screen);
                    }
                    else
                    {
                        foreach (var row in tick.Fresh)
                        {
                            output.Write(row);
                            output.Write('\n');
                        }
                    }
                    output.Flush();
                    shown = tick.Screen.Count;
                }
                cancel.WaitHandle.WaitOne(interval);
            }
        }
        catch (IOException)
        {
            // The reader closed the pipe: nobody is left to print to.
        }
    }

    /// <summary>
    /// Watches <paramref name="path"/>: the last <paramref name="count"/> lines numbered as
    /// <c>rtok logs</c> prints them, then each new line on top. A terminal is renumbered on each
    /// repaint; a pipe cannot renumber what it printed, so its numbers keep counting.
    /// </summary>
    /// <param name="path">The live log file.</param>
    /// <param name="count">How many lines the screen keeps.</param>
    /// <param name="output">Where rows go.</param>
    /// <param name="tty">Whether <paramref name="output"/> is a terminal: repaint in place.</param>
    /// <param name="colour">Colour the level.</param>
    /// <param name="cancel">Stops the watch.</param>
    public static void Run(
        string path,
        int count,
        TextWriter output,
        bool tty,
        bool colour,
        CancellationToken cancel
    )
    {
        var (follow, live) = LogFollow.Primed(path);
        using (follow)
        {
            var window = LogTail.With(live, path, count); // newest first
            var shown = window.Count;
            var first = true;
            Loop(
                output,
                tty,
                Poll,
                () =>
                {
                    if (first)
                    {
                        first = false;
                        var rows = LogScreen.Numbered(window, colour);
                        return new WatchTick(rows, rows);
                    }
                    var fresh = follow.Poll(path); // oldest first
                    if (fresh.Count == 0)
                    {
                        return new WatchTick([], LogScreen.Numbered(window, colour));
                    }
                    for (var i = 0; i < fresh.Count; i++)
                    {
                        window.Insert(0, fresh[i]);
                    }
                    if (window.Count > count)
                    {
                        window.RemoveRange(count, window.Count - count);
                    }
                    var appended = LogScreen.Numbered(fresh, colour, shown + 1);
                    shown += fresh.Count;
                    return new WatchTick(appended, LogScreen.Numbered(window, colour));
                },
                cancel
            );
        }
    }

    private static void Repaint(TextWriter output, int shown, IReadOnlyList<string> rows)
    {
        if (shown > 0)
        {
            output.Write($"\u001b[{shown}F"); // up `shown` rows, to column 1
        }
        output.Write("\u001b[J"); // clear from the cursor down
        foreach (var row in rows)
        {
            output.Write(row);
            output.Write('\n');
        }
    }
}
