namespace Rtok.Model;

/// <summary>A page shown as text until its data has its own shape.</summary>
/// <param name="Text">The text, every line ending in <c>\n</c>.</param>
/// <param name="Ready">False while the page waits on its source: not built yet, or still reading.</param>
public sealed record TextPage(string Text, bool Ready);
