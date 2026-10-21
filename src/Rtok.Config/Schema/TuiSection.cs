namespace Rtok.Config;

/// <summary>rtok tui (same data as rtok web)</summary>
/// <remarks>Read from [tui].</remarks>
public sealed class TuiSection
{
    /// <summary>"" = first tab (--tab &lt;page&gt;)</summary>
    public string Tab { get; set; } = "";

    /// <summary>model re-read cadence, same as the web 2 s tick (--tick-secs)</summary>
    public ulong TickSecs { get; set; } = 2;
}
