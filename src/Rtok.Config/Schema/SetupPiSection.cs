namespace Rtok.Config;

/// <summary>pi: the linked rtok extension</summary>
/// <remarks>Read from [setup.pi].</remarks>
public sealed class SetupPiSection
{
    /// <summary>pi's extensions folder</summary>
    public string ExtensionsPath { get; set; } = "~/.pi/agent/extensions";

    /// <summary>pi.registerTool for read/search/graph/memory</summary>
    public bool Tools { get; set; }
}
