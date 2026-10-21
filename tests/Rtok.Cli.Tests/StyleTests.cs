using Rtok.Cli.Ui;

namespace Rtok.Cli.Tests;

public sealed class StyleTests
{
    [Theory]
    [InlineData(LineKind.Success, "✅ ok x")]
    [InlineData(LineKind.Warn, "❗ ok x")]
    public void The_prefix_is_one_icon_padded_to_two_columns(LineKind kind, string line) =>
        Assert.Equal(line, Lines.Render(kind, "", "ok x", emoji: true, color: false));

    [Fact]
    public void Without_emoji_or_colour_the_line_is_the_bare_text() =>
        Assert.Equal("Error: z", Lines.Render(LineKind.Error, "", "Error: z", false, false));

    [Theory]
    [InlineData(LineKind.Success, "\e[32mok\e[0m")]
    [InlineData(LineKind.Info, "\e[36mok\e[0m")]
    [InlineData(LineKind.Warn, "\e[33mok\e[0m")]
    [InlineData(LineKind.Error, "\e[31mok\e[0m")]
    public void Each_kind_has_its_colour(LineKind kind, string line) =>
        Assert.Equal(line, Lines.Render(kind, "", "ok", emoji: false, color: true));

    [Theory]
    [InlineData("uninstall", "🧹")]
    [InlineData("installing", "📦")]
    [InlineData("indexed", "📚")]
    [InlineData("worktree removed", "🧹")]
    [InlineData("worktree add", "🌳")]
    [InlineData("started", "🚀")]
    [InlineData("stopping", "🛑")]
    [InlineData("bench", "🏁")]
    public void A_verb_takes_its_operations_icon(string verb, string icon) =>
        Assert.Equal(icon, Lines.Icon(verb, LineKind.Success));

    [Theory]
    [InlineData(LineKind.Success, "✅")]
    [InlineData(LineKind.Info, "💡")]
    [InlineData(LineKind.Warn, "❗")]
    [InlineData(LineKind.Error, "❌")]
    public void A_verb_no_row_names_falls_back_to_the_kind(LineKind kind, string icon)
    {
        Assert.Equal(icon, Lines.Icon("", kind));
        Assert.Equal(icon, Lines.Icon("frobnicate", kind));
    }

    [Theory]
    [InlineData("", true, true)]
    [InlineData("", false, false)]
    [InlineData("NO_COLOR=1", true, false)]
    [InlineData("NO_COLOR=", true, true)]
    [InlineData("TERM=dumb", true, false)]
    [InlineData("CLICOLOR=0", true, false)]
    [InlineData("CLICOLOR=1", false, false)]
    [InlineData("CLICOLOR_FORCE=1", false, true)]
    [InlineData("CLICOLOR_FORCE=0", false, false)]
    [InlineData("FORCE_COLOR=", false, true)]
    [InlineData("FORCE_COLOR=1", false, true)]
    [InlineData("FORCE_COLOR=0", true, false)]
    [InlineData("FORCE_COLOR=false", true, false)]
    [InlineData("FORCE_COLOR=1 NO_COLOR=1", false, true)]
    [InlineData("CLICOLOR_FORCE=1 TERM=dumb", true, true)]
    public void Colour_follows_the_variables_then_the_terminal(
        string variables,
        bool terminal,
        bool on
    )
    {
        var env = variables
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);

        Assert.Equal(on, ColorRule.Enabled(name => env.GetValueOrDefault(name), terminal));
    }
}
