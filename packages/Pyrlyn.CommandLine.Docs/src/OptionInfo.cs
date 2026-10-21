namespace Pyrlyn.CommandLine.Docs;

/// <summary>One option.</summary>
/// <param name="Names">Every spelling, short ones first: <c>-n</c>, <c>--lines</c>, <c>--limit</c>.</param>
/// <param name="Type">The value's C# type, or null for a flag that takes no value.</param>
/// <param name="Description">The description; empty when there is none.</param>
/// <param name="Default">The default as help shows it, a string's quotes removed; null when there is none or it is null.</param>
/// <param name="Required">True when help marks the option <c>[Required]</c>.</param>
public sealed record OptionInfo(
    IReadOnlyList<string> Names,
    string? Type,
    string Description,
    string? Default,
    bool Required
);
