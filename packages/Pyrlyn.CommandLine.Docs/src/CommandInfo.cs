namespace Pyrlyn.CommandLine.Docs;

/// <summary>One command.</summary>
/// <param name="Path">The words that select it, for example <c>config get</c>; <c>""</c> for the root.</param>
/// <param name="Aliases">Other full paths that run the same command (ConsoleAppFramework's <c>ls|list</c>).</param>
/// <param name="Description">The summary, one line per <c>\n</c>; empty when there is none.</param>
/// <param name="Arguments">The positional arguments, in order.</param>
/// <param name="Options">The options, global options included, in the order help prints them.</param>
public sealed record CommandInfo(
    string Path,
    IReadOnlyList<string> Aliases,
    string Description,
    IReadOnlyList<ArgumentInfo> Arguments,
    IReadOnlyList<OptionInfo> Options
);
