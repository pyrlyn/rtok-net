using ConsoleAppFramework;

namespace Pyrlyn.CommandLine.Docs.Tests;

/// <summary>The <c>stats</c> command.</summary>
// ConsoleAppFramework registers instance methods; this one does nothing with the instance.
#pragma warning disable CA1822
public sealed class StatsCommand
{
    /// <summary>Shows the savings.</summary>
    /// <param name="since">The window, for example <c>7d</c>.</param>
    /// <param name="by">Group the rows by this column.</param>
    /// <param name="color">Colour the output.</param>
    [Command("stats")]
    public void Stats(string since, string by = "day", bool color = true) => _ = (since, by, color);
}
#pragma warning restore CA1822
