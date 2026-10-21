using Pyrlyn.OperationIcons;
using Rtok.Config;
using Rtok.Process;
using Spectre.Console;

namespace Rtok.Cli.Ui;

/// <summary>
/// rtok's own human-facing lines: success and status on stdout, warnings and errors on stderr.
/// A line that names an operation (<c>index</c>, <c>start</c>, <c>remove</c>, …) takes that
/// operation's icon, any other its kind's. An icon needs <c>[ui] emoji</c> and a terminal on that
/// stream; colour needs <c>[ui] color</c> and <see cref="ColorRule"/>. What agents read (hook and
/// MCP JSON, <c>--json</c>, filtered command output, pipes) never comes through here.
/// </summary>
internal static class Lines
{
    /// <summary>rtok's own verbs, looked up after <see cref="VerbIcons.Shared"/>.</summary>
    public static IReadOnlyList<VerbIcon> OwnIcons { get; } =
    [
        new("index", "📚"),
        new("worktree", "🌳"),
        new("compress", "📉"),
        new("expand", "📂"),
        new("bench", "🏁"),
        new("start", "🚀"),
        new("stop", "🛑"),
    ];

    private static Lazy<UiSection> ui = new(() => ConfigLoader.Load().Config.Ui);

    /// <summary>Uses <paramref name="section"/> instead of loading the config on the first line.</summary>
    /// <param name="section">The <c>[ui]</c> section.</param>
    public static void Configure(UiSection section) => ui = new(section);

    /// <summary>A success line on stdout; <paramref name="verb"/> names the operation, if any.</summary>
    public static void Success(string text, string verb = "") =>
        Write(LineKind.Success, verb, text);

    /// <summary>A neutral status line on stdout.</summary>
    public static void Info(string text, string verb = "") => Write(LineKind.Info, verb, text);

    /// <summary>A warning line on stderr.</summary>
    public static void Warn(string text) => Write(LineKind.Warn, "", text);

    /// <summary>An error line on stderr.</summary>
    public static void Error(string text) => Write(LineKind.Error, "", text);

    /// <summary>The icon of <paramref name="verb"/>, else of <paramref name="kind"/>.</summary>
    public static string Icon(string verb, LineKind kind) =>
        VerbIcons.Icon(verb)
        ?? VerbIcons.Icon(OwnIcons, verb)
        ?? kind switch
        {
            LineKind.Success => "✅",
            LineKind.Info => "💡",
            LineKind.Warn => "❗",
            _ => "❌",
        };

    /// <summary>One line without its newline: the icon gutter when <paramref name="emoji"/>, the text painted when <paramref name="color"/>.</summary>
    public static string Render(LineKind kind, string verb, string text, bool emoji, bool color)
    {
        var gutter = emoji ? VerbIcons.Gutter(Icon(verb, kind)) : "";
        var colour = kind switch
        {
            LineKind.Success => Color.Green,
            LineKind.Info => Color.Teal,
            LineKind.Warn => Color.Olive,
            _ => Color.Maroon,
        };
        return gutter + (color ? Paint(text, colour) : text);
    }

    /// <summary><paramref name="text"/> in one of the 16 standard colours, as ANSI escapes.</summary>
    public static string Paint(string text, Color colour)
    {
        var console = AnsiConsole.Create(
            new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.Standard,
                Out = new AnsiConsoleOutput(TextWriter.Null),
            }
        );
        console.Profile.Width = int.MaxValue;
        return console.ToAnsi(new Text(text, new Style(colour)));
    }

    /// <summary>Whether rtok's own lines on stdout or stderr take colour: <c>[ui] color</c> and <see cref="ColorRule"/>.</summary>
    public static bool ColorOn(bool stderr) =>
        ui.Value.Color && ColorRule.Enabled(Environment.GetEnvironmentVariable, Terminal(stderr));

    private static bool Terminal(bool stderr) =>
        stderr ? !Console.IsErrorRedirected : !Console.IsOutputRedirected;

    private static void Write(LineKind kind, string verb, string text)
    {
        var stderr = kind is LineKind.Warn or LineKind.Error;
        var line = Render(kind, verb, text, ui.Value.Emoji && Terminal(stderr), ColorOn(stderr));
        ProgressDisplay.Suspend(() => (stderr ? Console.Error : Console.Out).WriteLine(line));
    }
}
