namespace Rtok.Config;

/// <summary>uniform JSON arrays rewritten as TOON</summary>
/// <remarks>Read from [plugins.toon].</remarks>
public sealed class ToonSection
{
    /// <summary>rewrite uniform JSON arrays as TOON</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>arrays shorter than this stay JSON</summary>
    public uint MinRows { get; set; } = 5;
}
