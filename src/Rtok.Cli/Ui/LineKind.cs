namespace Rtok.Cli.Ui;

/// <summary>What one of rtok's own lines reports; picks its icon, colour and stream together.</summary>
public enum LineKind
{
    /// <summary>Something worked (stdout, green, ✅).</summary>
    Success,

    /// <summary>A neutral status (stdout, cyan, 💡).</summary>
    Info,

    /// <summary>A warning (stderr, yellow, ❗).</summary>
    Warn,

    /// <summary>An error (stderr, red, ❌).</summary>
    Error,
}
