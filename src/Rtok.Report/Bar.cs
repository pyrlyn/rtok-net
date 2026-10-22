namespace Rtok.Report;

/// <summary>One bar.</summary>
/// <param name="Label">What it measures.</param>
/// <param name="Value">Its value; a negative one draws no bar.</param>
public sealed record Bar(string Label, long Value);
