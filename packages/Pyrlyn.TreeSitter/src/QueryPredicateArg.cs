namespace Pyrlyn.TreeSitter;

/// <summary>An argument of a general predicate: a capture index or a string.</summary>
/// <param name="CaptureId">The capture, or null for a string.</param>
/// <param name="Text">The string, or null for a capture.</param>
public readonly record struct QueryPredicateArg(int? CaptureId, string? Text);
