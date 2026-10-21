using Pyrlyn.CommandLine.Docs;
using Rtok.Testing;

namespace Rtok.Model.Tests;

public sealed class ParityTests
{
    private static readonly OptionInfo Json = new(["--json"], null, "Print JSON.", null, false);

    [Fact]
    public void Every_reading_command_has_a_web_and_a_tui_page_or_an_exemption()
    {
        var model = CommandModelJson.Deserialize(
            File.ReadAllText(Path.Join(RepoPaths.Root, "docs", "cli", "commands.json"))
        );

        Assert.Contains(model.Commands, ParityMap.IsReading);
        Assert.Empty(ParityMap.Check(model));
    }

    [Fact]
    public void A_reading_command_without_a_page_fails()
    {
        var model = new CommandModel(
            "rtok",
            [Command("plugins", Json), Command("stats", Json), Command("man")]
        );

        Assert.Equal(
            ["rtok stats reads but has no web or TUI page and no exemption"],
            ParityMap.Check(model, Pages("plugins"), Exempt())
        );
    }

    [Fact]
    public void An_exemption_or_a_page_for_a_gone_command_fails()
    {
        var model = new CommandModel("rtok", [Command("plugins", Json)]);

        Assert.Equal(
            [
                "rtok config show has pages but no such command",
                "rtok gone is exempt but no such command",
            ],
            ParityMap.Check(model, Pages("plugins", "config show"), Exempt("gone"))
        );
    }

    [Fact]
    public void An_exempt_reading_command_passes()
    {
        var model = new CommandModel("rtok", [Command("stats", Json)]);

        Assert.Empty(ParityMap.Check(model, Pages(), Exempt("stats")));
    }

    private static CommandInfo Command(string path, params OptionInfo[] options) =>
        new(path, [], "", [], options);

    private static Dictionary<string, PageLink> Pages(params string[] paths) =>
        paths.ToDictionary(
            p => p,
            _ => new PageLink(Page.Plugins, Page.Plugins),
            StringComparer.Ordinal
        );

    private static Dictionary<string, string> Exempt(params string[] paths) =>
        paths.ToDictionary(p => p, _ => "a reason", StringComparer.Ordinal);
}
