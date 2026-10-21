namespace Pyrlyn.CommandLine.Docs;

/// <summary>One positional argument. Help names its type, not its parameter.</summary>
/// <param name="Type">The C# type as help prints it: <c>string</c>, <c>int?</c>, <c>string[]</c>.</param>
/// <param name="Description">The description; empty when there is none.</param>
public sealed record ArgumentInfo(string Type, string Description);
