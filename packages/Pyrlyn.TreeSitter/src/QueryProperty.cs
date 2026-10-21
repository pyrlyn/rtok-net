namespace Pyrlyn.TreeSitter;

/// <summary>A <c>#set!</c> or <c>#is?</c> property: a key, an optional value and capture.</summary>
/// <param name="Key">The property name.</param>
/// <param name="Value">The value, when given.</param>
/// <param name="CaptureId">The capture the property is about, when given.</param>
public sealed record QueryProperty(string Key, string? Value, int? CaptureId);
