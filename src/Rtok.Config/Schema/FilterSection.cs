namespace Rtok.Config;

/// <summary>rtok filter --stdin</summary>
/// <remarks>Read from [filter].</remarks>
public sealed class FilterSection
{
    /// <summary>command family hint when the caller knows it (--cmd)</summary>
    public string Cmd { get; set; } = "";
}
