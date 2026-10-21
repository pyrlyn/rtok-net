using System.Text;
using Pyrlyn.RotatingLog;

namespace Rtok.Log;

/// <summary>The last lines of the log across its rotated files, newest first.</summary>
public static class LogTail
{
    /// <summary>
    /// The last <paramref name="count"/> lines of <paramref name="path"/>, then of <c>.1</c>,
    /// <c>.2</c> and so on, newest first. Rotation is contiguous, so the walk stops at the first
    /// missing file. Empty when nothing has been logged.
    /// </summary>
    /// <param name="path">The live log file.</param>
    /// <param name="count">How many lines.</param>
    /// <returns>The lines, newest first.</returns>
    public static IReadOnlyList<string> Read(string path, int count) =>
        With(WholeLinesOf(path), path, count);

    /// <summary>
    /// <see cref="Read"/> with the live file's lines given: <c>logs watch</c> reads the live file
    /// once for both its first screen and its follow state, so a line written as it starts shows
    /// exactly once.
    /// </summary>
    internal static List<string> With(IReadOnlyList<string> live, string path, int count)
    {
        var lines = live.Reverse().Take(count).ToList();
        for (var i = 1; lines.Count < count; i++)
        {
            var older = RotatingFileLog.RotatedPath(path, i);
            if (!File.Exists(older))
            {
                break;
            }
            lines.AddRange(WholeLinesOf(older).AsEnumerable().Reverse().Take(count - lines.Count));
        }
        return lines;
    }

    /// <summary>
    /// The whole lines of <paramref name="bytes"/>: the piece after the last newline is a line
    /// still being written. Invalid UTF-8 costs a character, not the log.
    /// </summary>
    internal static List<string> WholeLines(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.LastIndexOf((byte)'\n');
        if (end < 0)
        {
            return [];
        }
        return [.. Encoding.UTF8.GetString(bytes[..end]).Split('\n')];
    }

    private static List<string> WholeLinesOf(string path)
    {
        try
        {
            return WholeLines(File.ReadAllBytes(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
