namespace Pyrlyn.CommandLine.Docs;

/// <summary>Every command of a ConsoleAppFramework program, as its <c>--help</c> describes it.</summary>
/// <param name="Name">The program's name, for example <c>rtok</c>; <c>--help</c> does not print it.</param>
/// <param name="Commands">The root command (path <c>""</c>) first, then every command the root help lists, in its order.</param>
public sealed record CommandModel(string Name, IReadOnlyList<CommandInfo> Commands);
