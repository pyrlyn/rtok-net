namespace Rtok.Config;

/// <summary>the measure plugin</summary>
/// <remarks>Read from [plugins.measure].</remarks>
public sealed class MeasureSection
{
    /// <summary>false = the measure plugin is off</summary>
    public bool Enabled { get; set; } = true;
}
