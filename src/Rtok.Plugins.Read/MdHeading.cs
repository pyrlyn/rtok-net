namespace Rtok.Plugins.Read;

/// <summary>One ATX heading of a Markdown file.</summary>
/// <param name="Line">1-based line.</param>
/// <param name="Level">Number of leading <c>#</c>, 1 to 6.</param>
/// <param name="Text">The whole heading line, trimmed.</param>
/// <param name="Body">The first non-empty line after the heading, trimmed.</param>
public sealed record MdHeading(int Line, int Level, string Text, string? Body);
