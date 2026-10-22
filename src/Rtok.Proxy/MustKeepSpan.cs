namespace Rtok.Proxy;

/// <summary>One verbatim substring a compressor summary has to carry.</summary>
/// <param name="Kind">One of <see cref="MustKeep.Kinds"/>.</param>
/// <param name="Text">The span, at most <see cref="MustKeep.SpanChars"/> characters.</param>
internal sealed record MustKeepSpan(string Kind, string Text);
