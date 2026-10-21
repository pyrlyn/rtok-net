using System.Globalization;

namespace Rtok.Log;

/// <summary>How <c>rtok logs</c> shows lines: numbered <c>1</c> newest, the level coloured.</summary>
public static class LogScreen
{
    /// <summary>What <c>rtok logs</c> and <c>logs export</c> print when there is nothing.</summary>
    public const string Empty = "no logs yet";

    /// <summary>The lines numbered from <paramref name="first"/>, coloured when asked.</summary>
    /// <param name="lines">The lines, newest first.</param>
    /// <param name="colour">Colour the level (a terminal that allows it).</param>
    /// <param name="first">The number of the first line.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<string> Numbered(
        IEnumerable<string> lines,
        bool colour,
        int first = 1
    ) =>
        [
            .. lines.Select(
                (line, i) =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{first + i} {(colour ? Colour(line) : line)}"
                    )
            ),
        ];

    /// <summary>
    /// A line (<c>&lt;date&gt; &lt;time&gt; &lt;level&gt; rest</c>) with its level coloured: red
    /// error, yellow warn, dim debug, info plain. A line of another shape comes back unchanged.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The line with ANSI colour.</returns>
    public static string Colour(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var parts = line.Split(' ', 4);
        if (parts.Length < 4)
        {
            return line;
        }
        var code = parts[2] switch
        {
            "error" => "31",
            "warn" => "33",
            "debug" => "2",
            _ => null,
        };
        return code is null
            ? line
            : $"{parts[0]} {parts[1]} \u001b[{code}m{parts[2]}\u001b[0m {parts[3]}";
    }
}
