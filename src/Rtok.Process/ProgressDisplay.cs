namespace Rtok.Process;

/// <summary>
/// Where indicators are drawn. Nothing is drawn until a surface that owns a terminal installs a
/// display, so hooks, MCP and the proxy stay silent by default; <see cref="Disable"/> and
/// <c>RTOK_NO_PROGRESS=1</c> turn drawing off for the process.
/// </summary>
public static class ProgressDisplay
{
    /// <summary>Set to <c>1</c>, nothing is drawn.</summary>
    public const string OffVariable = "RTOK_NO_PROGRESS";

    private static volatile IProgressDisplay? installed;
    private static volatile bool disabled;

    /// <summary>Draws through <paramref name="display"/> from now on, unless the process turned drawing off.</summary>
    /// <param name="display">The display.</param>
    public static void Install(IProgressDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);
        installed = display;
    }

    /// <summary>Turns every indicator off for the process: stderr belongs to a host, not a person.</summary>
    public static void Disable() => disabled = true;

    /// <summary>Back to the start: nothing installed, nothing turned off. Tests only.</summary>
    internal static void Reset()
    {
        installed = null;
        disabled = false;
    }

    /// <summary>Hides every live indicator while <paramref name="write"/> runs, and redraws them after.</summary>
    /// <param name="write">The work.</param>
    public static void Suspend(Action write)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (Active is { } display)
        {
            display.Suspend(write);
        }
        else
        {
            write();
        }
    }

    internal static IIndicator Spinner(string label) =>
        Active?.Spinner(label) ?? HiddenIndicator.Instance;

    internal static IIndicator Bar(string label, long total, ProgressUnit unit) =>
        Active?.Bar(label, total, unit) ?? HiddenIndicator.Instance;

    /// <summary>The rule: a display is installed, the process has not turned it off, and the variable is not <c>1</c>.</summary>
    internal static bool Shown(bool hasDisplay, bool off, string? variable) =>
        hasDisplay && !off && variable is not "1";

    private static IProgressDisplay? Active =>
        Shown(
            installed is not null,
            disabled,
            System.Environment.GetEnvironmentVariable(OffVariable)
        )
            ? installed
            : null;
}
