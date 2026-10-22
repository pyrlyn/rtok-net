namespace Rtok.Report;

/// <summary>One recommendation.</summary>
/// <param name="Rule">The rule that found it.</param>
/// <param name="Text">What it found and what to do.</param>
/// <param name="Evidence">The rows it rests on.</param>
/// <param name="Tokens">The estimated tokens following it recovers.</param>
public sealed record Finding(string Rule, string Text, string Evidence, long Tokens);
