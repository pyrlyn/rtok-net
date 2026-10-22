using Rtok.Config;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>One surface's calls in the window and the latency of those that recorded one.</summary>
public sealed record SurfaceLatency(
    string Surface,
    long Calls,
    long Timed,
    double? P50,
    double? P95
);
