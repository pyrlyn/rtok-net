namespace Rtok.Cli.Ui;

/// <summary>
/// Whether a stream takes colour. <c>FORCE_COLOR</c> decides first (<c>0</c> or <c>false</c> off,
/// any other value on), then <c>CLICOLOR_FORCE</c> other than <c>0</c> turns it on; otherwise
/// <c>NO_COLOR</c>, <c>TERM=dumb</c> or <c>CLICOLOR=0</c> turn it off, and a terminal turns it on.
/// </summary>
internal static class ColorRule
{
    /// <summary>The rule for one stream.</summary>
    /// <param name="environment">Reads a variable; null when unset.</param>
    /// <param name="terminal">Whether the stream is a terminal.</param>
    /// <returns>Whether to colour it.</returns>
    public static bool Enabled(Func<string, string?> environment, bool terminal)
    {
        var force = environment("FORCE_COLOR");
        if (force is "0" or "false")
        {
            return false;
        }
        if (force is not null || environment("CLICOLOR_FORCE") is { Length: > 0 } and not "0")
        {
            return true;
        }
        return terminal
            && string.IsNullOrEmpty(environment("NO_COLOR"))
            && environment("TERM") is not "dumb"
            && environment("CLICOLOR") is not "0";
    }
}
