using Rtok.Cli.Shell;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// NUL-split argv against rtok's command model through <see cref="CommandPath"/>, the check that
/// turns what ConsoleAppFramework would answer with the root help into exit 2: it never throws,
/// leaves <c>--help</c> and <c>--version</c> alone, names the offending word, and suggests a help
/// command that walks the words before it and passes the check itself.
/// </summary>
internal static class CliArgv
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var args = FuzzInput.Text(data).Split('\0');
        var model = CommandDocs.Model;
        var error = CommandPath.Check(model, args);
        var asksHelp = args.TakeWhile(a => a is not "--")
            .Any(a => a is "-h" or "--help" or "--version");
        FuzzInput.Check(!asksHelp || error is null, "--help or --version was refused");
        if (error is not var (message, help))
        {
            return;
        }
        FuzzInput.Check(
            args.Any(a => message.Contains($"'{a}'", StringComparison.Ordinal)),
            $"{message} names no argument"
        );
        // An argument may hold spaces, so compare the joined path, not the words.
        var path = help.EndsWith(" --help", StringComparison.Ordinal)
            ? help[..^" --help".Length]
            : null;
        FuzzInput.Check(
            path is not null
                && Enumerable
                    .Range(0, args.Length)
                    .Any(k =>
                        string.Equals(
                            path,
                            string.Join(' ', [model.Name, .. args.Take(k)]),
                            StringComparison.Ordinal
                        )
                    ),
            $"{help} does not walk the words before the error"
        );
        string[] words = help.Split(' ');
        FuzzInput.Check(CommandPath.Check(model, words[1..]) is null, $"{help} is refused itself");
    }
}
