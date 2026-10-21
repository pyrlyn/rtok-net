namespace Rtok.Config;

/// <summary>rtok hook &lt;event&gt;</summary>
/// <remarks>Read from [hook].</remarks>
public sealed class HookSection
{
    /// <summary>claude | cursor | copilot | devin | cline — payload field mapping</summary>
    public string Host { get; set; } = "claude";

    /// <summary>soft budget; over it, the event is logged as slow</summary>
    public ulong MaxMs { get; set; } = 10;

    /// <summary>any error → `{}` and exit 0; false only for debugging</summary>
    public bool FailOpen { get; set; } = true;
}
