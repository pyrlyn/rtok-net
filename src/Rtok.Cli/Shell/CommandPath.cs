using Pyrlyn.CommandLine.Docs;

namespace Rtok.Cli.Shell;

/// <summary>
/// What ConsoleAppFramework does not reject: a word that names no command, or an option, where
/// only a command can follow (after <c>rtok</c> or a group such as <c>rtok config</c>). It
/// answers those with the root help and exit 0; rtok makes them errors. A bare group still
/// prints its help, and any <c>--help</c> or <c>--version</c> is left to ConsoleAppFramework.
/// </summary>
internal static class CommandPath
{
    /// <summary>The error for <paramref name="args"/>, or null when ConsoleAppFramework should run them.</summary>
    /// <param name="model">The command model.</param>
    /// <param name="args">The arguments after the program name.</param>
    /// <returns>The error and the help command to suggest, or null.</returns>
    public static (string Message, string Help)? Check(
        CommandModel model,
        IReadOnlyList<string> args
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(args);
        if (args.TakeWhile(a => a is not "--").Any(a => a is "-h" or "--help" or "--version"))
        {
            return null;
        }
        var runnable = model
            .Commands.SelectMany(c => (string[])[c.Path, .. c.Aliases])
            .Where(p => p.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (
            model.Commands.Any(c =>
                c.Path.Length == 0 && (c.Options.Count > 0 || c.Arguments.Count > 0)
            )
        )
        {
            // A root command of its own parses whatever follows.
            runnable.Add("");
        }
        var groups = runnable.SelectMany(Prefixes).ToHashSet(StringComparer.Ordinal);
        var path = "";
        foreach (var arg in args)
        {
            var next = path.Length == 0 ? arg : $"{path} {arg}";
            if (!arg.StartsWith('-') && (runnable.Contains(next) || groups.Contains(next)))
            {
                path = next;
                continue;
            }
            if (runnable.Contains(path))
            {
                return null;
            }
            var help = path.Length == 0 ? $"{model.Name} --help" : $"{model.Name} {path} --help";
            return arg.StartsWith('-')
                ? ($"unexpected option '{arg}'", help)
                : ($"unknown command '{arg}'", help);
        }
        return null;
    }

    private static IEnumerable<string> Prefixes(string path)
    {
        for (
            var space = path.IndexOf(' ', StringComparison.Ordinal);
            space > 0;
            space = path.IndexOf(' ', space + 1)
        )
        {
            yield return path[..space];
        }
    }
}
