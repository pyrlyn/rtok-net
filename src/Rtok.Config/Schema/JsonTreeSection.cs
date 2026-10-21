namespace Rtok.Config;

/// <summary>nested JSON folding in old tool results</summary>
/// <remarks>Read from [plugins.json_tree].</remarks>
public sealed class JsonTreeSection
{
    /// <summary>fold repeated nested JSON in old tool results; off until a measurement shows a saving</summary>
    public bool Enabled { get; set; }
}
