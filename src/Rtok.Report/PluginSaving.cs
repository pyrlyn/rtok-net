using Rtok.Config;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>Net estimated tokens one plugin's <c>measurements</c> rows saved: est_before −
/// est_after, so an <c>expand</c> row counts negative.</summary>
public sealed record PluginSaving(string Plugin, long Rows, long EstBefore, long EstAfter)
{
    /// <summary>est_before − est_after, saturating.</summary>
    public long Saved => Saturating.Sub(EstBefore, EstAfter);
}
