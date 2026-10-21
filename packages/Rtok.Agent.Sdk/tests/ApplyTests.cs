namespace Rtok.Agent.Sdk.Tests;

public sealed class ApplyTests
{
    [Theory]
    [InlineData("mcpServers.rtok: rtok mcp", true)]
    [InlineData("no changes", false)]
    [InlineData("", false)]
    [InlineData("leave mcpServers.rtok in x (not rtok's; remove by hand)", false)]
    [InlineData("? mcpServers.rtok in x (changed by you; remove asks)\nleave y", false)]
    [InlineData("leave y\n- mcpServers.rtok", true)]
    public void ReportIsTheWriteGate(string report, bool writes)
    {
        Assert.Equal(writes, new Apply().Writes(report));
        Assert.False(new Apply { DryRun = true }.Writes(report));
    }

    [Fact]
    public void WithoutATerminalEveryQuestionIsANo()
    {
        var apply = new Apply();
        Assert.False(apply.Confirmed("remove it?"));
        Assert.False(apply.Accepted("install it?"));
    }

    [Fact]
    public void YesAcceptsWithoutAsking()
    {
        var asked = 0;
        var apply = new Apply
        {
            Yes = true,
            Ask = (_, _) =>
            {
                asked++;
                return false;
            },
        };
        Assert.True(apply.Confirmed("remove it?"));
        Assert.True(apply.Accepted("install it?"));
        Assert.Equal(0, asked);
    }

    [Fact]
    public void TheTerminalGetsTheQuestionAndItsDefault()
    {
        var seen = new List<(string, bool)>();
        var apply = new Apply
        {
            Ask = (q, d) =>
            {
                seen.Add((q, d));
                return true;
            },
        };
        Assert.True(apply.Confirmed("remove it?"));
        Assert.True(apply.Accepted("install it?"));
        Assert.Equal([("remove it?", false), ("install it?", true)], seen);
    }

    [Theory]
    [InlineData("", true, true)]
    [InlineData("\n", false, false)]
    [InlineData("y\n", false, true)]
    [InlineData(" YES ", false, true)]
    [InlineData("n", true, false)]
    [InlineData("yep", true, false)]
    [InlineData(null, true, false)]
    public void AnswerTakesTheDefaultOnEnterAndNeverActsUnanswered(
        string? line,
        bool defaultYes,
        bool accepted
    ) => Assert.Equal(accepted, Prompts.Answer(line, defaultYes));

    [Fact]
    public void QuestionLineShowsTheDefault()
    {
        Assert.Equal(
            "? remove x? you changed it [y/N] ",
            Prompts.Line("remove x? you changed it", false)
        );
        Assert.Equal("? install x? [Y/n] ", Prompts.Line("install x?", true));
    }
}
