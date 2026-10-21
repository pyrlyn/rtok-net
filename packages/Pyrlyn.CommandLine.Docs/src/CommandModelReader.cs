namespace Pyrlyn.CommandLine.Docs;

/// <summary>
/// Reads a ConsoleAppFramework program's command model from its <c>--help</c> output: the root
/// page, then the page of every command the root lists.
/// </summary>
/// <remarks>
/// ConsoleAppFramework generates its parser at compile time and keeps no command tree at
/// runtime, so the help text is the only description of the program there is. The reader is
/// strict: a line it does not recognise, or a page that is not the one it asked for (an unknown
/// path prints the root help), throws <see cref="HelpFormatException"/>.
/// </remarks>
public static class CommandModelReader
{
    /// <summary>Reads the model.</summary>
    /// <param name="name">The program's name, for example <c>rtok</c>.</param>
    /// <param name="runHelp">
    /// Runs the built program with these arguments (a command path, then <c>--help</c>) and
    /// returns its standard output. The caller owns starting processes.
    /// </param>
    /// <returns>The model, root first.</returns>
    /// <exception cref="HelpFormatException">A page is not in the format, or not the page asked for.</exception>
    public static CommandModel Read(string name, Func<IReadOnlyList<string>, string> runHelp)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(runHelp);

        var root = Page(runHelp, []);
        if (root.Usage.Length != 0)
        {
            throw new HelpFormatException(
                $"the root help names a command in its usage line: \"{root.Usage}\""
            );
        }
        var commands = new List<CommandInfo>
        {
            new("", [], root.Description, root.Arguments, root.Options),
        };
        foreach (var (names, _) in root.Commands)
        {
            var path = names[0];
            var page = Page(runHelp, path.Split(' '));
            var expected = string.Join('|', names);
            if (!string.Equals(page.Usage, expected, StringComparison.Ordinal))
            {
                throw new HelpFormatException(
                    $"\"{path} --help\" printed the help of \"{page.Usage}\", not \"{expected}\""
                );
            }
            if (page.Commands.Count != 0)
            {
                throw new HelpFormatException($"\"{path} --help\" lists commands");
            }
            commands.Add(
                new CommandInfo(
                    path,
                    names.Skip(1).ToList(),
                    page.Description,
                    page.Arguments,
                    page.Options
                )
            );
        }
        return new CommandModel(name, commands);
    }

    private static HelpPage Page(Func<IReadOnlyList<string>, string> runHelp, string[] path)
    {
        string[] args = [.. path, "--help"];
        try
        {
            return HelpPage.Parse(runHelp(args));
        }
        catch (HelpFormatException e)
        {
            throw new HelpFormatException($"\"{string.Join(' ', args)}\": {e.Message}", e);
        }
    }
}
