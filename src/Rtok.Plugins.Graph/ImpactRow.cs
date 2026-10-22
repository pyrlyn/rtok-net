namespace Rtok.Plugins.Graph;

/// <summary>One definition the <c>impact</c> walk reached: its depth, file, and name
/// (<c>""</c> for a file-level reference).</summary>
internal readonly record struct ImpactRow(int Depth, string Path, string Scope);
