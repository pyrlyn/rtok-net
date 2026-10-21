using Pyrlyn.CommandLine.Docs;

namespace Rtok.Cli.Shell;

/// <summary>
/// rtok's command model, <c>docs/cli/commands.json</c> embedded at build. ConsoleAppFramework
/// keeps no command tree at run time; this one is read from the binary's own <c>--help</c>
/// and kept in step by a drift test.
/// </summary>
internal static class CommandDocs
{
    private static readonly Lazy<CommandModel> Loaded = new(Load);

    /// <summary>The model.</summary>
    public static CommandModel Model => Loaded.Value;

    /// <summary>The completion script of each shell, by the name <c>rtok completions</c> takes.</summary>
    public static IReadOnlyList<string> Shells { get; } =
    ["bash", "zsh", "fish", "powershell", "clink"];

    /// <summary>The completion script for <paramref name="shell"/>, one of <see cref="Shells"/>.</summary>
    /// <param name="shell">The shell.</param>
    /// <returns>The script.</returns>
    public static string Script(string shell) =>
        shell switch
        {
            "bash" => CompletionScripts.Bash(Model),
            "zsh" => CompletionScripts.Zsh(Model),
            "fish" => CompletionScripts.Fish(Model),
            "powershell" => CompletionScripts.PowerShell(Model),
            "clink" => CompletionScripts.Clink(Model),
            _ => throw new ArgumentOutOfRangeException(
                nameof(shell),
                shell,
                "not a shell rtok completes for"
            ),
        };

    private static CommandModel Load()
    {
        using var stream =
            typeof(CommandDocs).Assembly.GetManifestResourceStream("commands.json")
            ?? throw new InvalidOperationException("the command model is not embedded in rtok");
        using var reader = new StreamReader(stream);
        return CommandModelJson.Deserialize(reader.ReadToEnd());
    }
}
