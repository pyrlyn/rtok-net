namespace Rtok.Proxy;

/// <summary>A compressor job's outcome and its reason, error or dropped spans.</summary>
internal readonly record struct SummaryResult(SummaryOutcome Outcome, string Detail = "");
