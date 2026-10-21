namespace Pyrlyn.CommandLine.Docs.Tests;

public sealed class CommandModelTests
{
    private static readonly Lazy<CommandModel> Sample = new(SampleApp.Read);

    [Fact]
    public void TheSampleModelMatchesItsGolden()
    {
        var json = CommandModelJson.Serialize(Sample.Value);
        Goldens.Match("sample.json", json);
        Assert.Null(CommandModelJson.Diff(Goldens.Read("sample.json"), Sample.Value));
    }

    [Fact]
    public void ReadsEveryShapeOfTheHelp()
    {
        var model = Sample.Value;
        Assert.Equal<string>(
            ["", "config", "config get", "config set", "stats", "worktree list", "worktree remove"],
            model.Commands.Select(c => c.Path).Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        var root = Command(model, "");
        Assert.Equal("Shows the sample's state.", root.Description);
        Assert.Contains(
            root.Options,
            o => o.Names.SequenceEqual(["--dry-run"], StringComparer.Ordinal) && o.Type is null
        );
        Assert.Contains(
            root.Options,
            o => o.Names.SequenceEqual(["-v", "--verbose"], StringComparer.Ordinal)
        );

        var set = Command(model, "config set");
        // An alias is a whole path of its own: "sample put", not "sample config put".
        Assert.Equal<string>(["put"], set.Aliases, StringComparer.Ordinal);
        Assert.Equal<string>(
            ["string", "string?"],
            set.Arguments.Select(a => a.Type),
            StringComparer.Ordinal
        );
        Assert.Equal("The value; the key is removed without one.", set.Arguments[1].Description);
        Assert.Contains(
            set.Options,
            o => o.Names.SequenceEqual(["-f", "--yes", "--force"], StringComparer.Ordinal)
        );

        var remove = Command(model, "worktree remove");
        Assert.Equal<string>(["rm"], remove.Aliases, StringComparer.Ordinal);
        Assert.Equal("Removes worktrees.\nTheir branches stay.", remove.Description);
        var since = Option(remove, "--since");
        Assert.Equal(("string", "7d", false), (since.Type, since.Default, since.Required));
        Assert.Null(Option(remove, "--ratio").Default);

        var stats = Command(model, "stats");
        Assert.True(Option(stats, "--since").Required);
        Assert.Equal("true", Option(stats, "--color").Default);
        Assert.Equal("Text", Option(Command(model, "config"), "--format").Default);
        Assert.Equal("10", Option(Command(model, "worktree list"), "--limit").Default);
    }

    [Fact]
    public void AChangedDocCommentIsDrift()
    {
        var diff = CommandModelJson.Diff(Goldens.Read("sample.json"), SampleApp.ReadEdited());
        Assert.NotNull(diff);
        Assert.StartsWith("command model drift at line ", diff, StringComparison.Ordinal);
        Assert.Contains("Group the rows by this column or tag.", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void TheJsonRoundTrips()
    {
        var json = CommandModelJson.Serialize(Sample.Value);
        Assert.Equal(json, CommandModelJson.Serialize(CommandModelJson.Deserialize(json)));
        Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ManPagesMatchTheirGoldens() => Goldens.Match("man", ManPages.Render(Sample.Value));

    [Fact]
    public void CompletionScriptsMatchTheirGoldens() =>
        Goldens.Match("completions", CompletionScripts.Render(Sample.Value));

    [Fact]
    public void AnUnknownPathPrintingTheRootHelpFailsTheReader()
    {
        // A program that lost its commands answers every "<path> --help" with the root page.
        var root = SampleApp.Run(["--help"]);
        var e = Assert.Throws<HelpFormatException>(() =>
            CommandModelReader.Read("sample", _ => root)
        );
        Assert.Contains(
            "\"config --help\" printed the help of \"\"",
            e.Message,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("Usage: [-h|--help] [--version]\nShows.\n", "help line 2: expected a blank line")]
    [InlineData("usage: x\n", "help line 1: expected \"Usage: ...\"")]
    [InlineData(
        "Usage: [-h|--help] [--version]\n\nOptions:\n  --a    A.\n\nArguments:\n  [0] <string>    B.\n",
        "help line 6: sections out of order"
    )]
    [InlineData(
        "Usage: [-h|--help] [--version]\n\nOptions:\n  a <int>    A.\n",
        "help line 4: expected \"-x, --name <type>\""
    )]
    [InlineData(
        "Usage: [-h|--help] [--version]\n\nOptions:\n  --a    A.\n x\n",
        "help line 5: not a row of the Options section"
    )]
    public void MalformedHelpFailsTheReader(string page, string message)
    {
        var e = Assert.Throws<HelpFormatException>(() => CommandModelReader.Read("x", _ => page));
        Assert.Contains(message, e.Message, StringComparison.Ordinal);
        Assert.StartsWith("\"--help\": ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommandPageThatListsCommandsFailsTheReader()
    {
        const string Root = "Usage: [command] [-h|--help] [--version]\n\nCommands:\n  a    A.\n";
        const string Page = "Usage: a [-h|--help] [--version]\n\nCommands:\n  b    B.\n";
        var e = Assert.Throws<HelpFormatException>(() =>
            CommandModelReader.Read("x", args => args.Count == 1 ? Root : Page)
        );
        Assert.Contains("lists commands", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesVerbatimAndEmptyStringDefaults()
    {
        const string Page =
            "Usage: [-h|--help] [--version]\n\nOptions:\n"
            + "  --a <string>    Quoted. [Default: @\"say \"\"hi\"\"\"]\n"
            + "  --b <string>    [Default: @\"\"]\n"
            + "  --c <int>       Over\n"
            + "                  two lines. [Required]\n";
        var options = CommandModelReader.Read("x", _ => Page).Commands[0].Options;
        Assert.Equal(("Quoted.", "say \"hi\""), (options[0].Description, options[0].Default));
        Assert.Equal(("", ""), (options[1].Description, options[1].Default));
        Assert.Equal(("Over\ntwo lines.", true), (options[2].Description, options[2].Required));
    }

    [Theory]
    [InlineData("The window, for example <c>7d</c>.", "The window, for example 7d.")]
    [InlineData(
        "See <see cref=\"T:Ns.Thing\"/> or <see cref=\"Other\" />.",
        "See Ns.Thing or Other."
    )]
    [InlineData("Unlike <paramref name=\"key\"/>, <b>never</b> null.", "Unlike key, never null.")]
    [InlineData("Tom &amp; Jerry &lt;3", "Tom & Jerry <3")]
    [InlineData("a < b", "a < b")]
    public void DocCommentMarkupBecomesPlainText(string raw, string plain)
    {
        var page = $"Usage: [-h|--help] [--version]\n\n{raw}\n\nOptions:\n  --a    {raw}\n";
        var root = CommandModelReader.Read("x", _ => page).Commands[0];
        Assert.Equal(plain, root.Description);
        Assert.Equal(plain, root.Options[0].Description);
    }

    private static CommandInfo Command(CommandModel model, string path) =>
        model.Commands.Single(c => string.Equals(c.Path, path, StringComparison.Ordinal));

    private static OptionInfo Option(CommandInfo command, string name) =>
        command.Options.Single(o => o.Names.Contains(name, StringComparer.Ordinal));
}
