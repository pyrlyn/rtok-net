using System.Globalization;

namespace Rtok.Process;

internal static class ProgressText
{
    /// <summary>The first <c>NN%</c> in <paramref name="line"/>: the value, where its digits start and where the sign ends.</summary>
    internal static (long Value, int Start, int End)? PercentIn(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var at = line.IndexOf('%', StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }
        var start = at;
        while (start > 0 && char.IsAsciiDigit(line[start - 1]))
        {
            start--;
        }
        return TryCount(line[start..at], out var value) && value <= 100
            ? (value, start, at + 1)
            : null;
    }

    internal static bool TryCount(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
