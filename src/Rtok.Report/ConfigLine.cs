using Rtok.Measure;

namespace Rtok.Report;

/// <summary>A config key as the Config section shows it.</summary>
public sealed record ConfigLine(string Key, string Value, string Source);
