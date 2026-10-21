namespace Rtok.Config;

/// <summary>rtok web (same data as rtok tui)</summary>
/// <remarks>Read from [web].</remarks>
public sealed class WebSection
{
    /// <summary>--host</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>--port</summary>
    public ushort Port { get; set; } = 3333;
}
