using Rtok.Config;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>One hook event's calls in the window and how many of them a <c>measurements</c>
/// row points at.</summary>
public sealed record HookActivity(string Name, long Calls, long Measured);
