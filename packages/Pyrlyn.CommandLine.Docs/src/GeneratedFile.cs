namespace Pyrlyn.CommandLine.Docs;

/// <summary>A generated file: a man page or a completion script.</summary>
/// <param name="Name">The file name, for example <c>rtok-config-get.1</c>.</param>
/// <param name="Content">The text, <c>\n</c> line ends, ending in a newline.</param>
public sealed record GeneratedFile(string Name, string Content);
