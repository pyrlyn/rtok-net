using Rtok.Config;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>Where tool output costs the most: bytes before rtok per actionable class and sink.</summary>
public sealed record Sink(string Class, string Name, long Bytes, long Rows);
