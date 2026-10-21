namespace Pyrlyn.ChangePreview.Tests;

public sealed class PreviewTests
{
    [Fact]
    public void EditCountsLinesAndRendersAUnifiedDiff()
    {
        var p = new Preview().Edit("c.toml", "a\nb\n", "a\nc\nd\n");

        var e = Assert.Single(p.Edits);
        Assert.Equal((2, 1), (e.Insertions, e.Deletions));
        Assert.StartsWith("--- a/c.toml\n+++ b/c.toml\n@@ ", e.Diff, StringComparison.Ordinal);
        Assert.Contains("-b\n+c\n", e.Diff, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEditThatChangesNothingIsDropped()
    {
        var p = new Preview().Edit("c.toml", "same\n", "same\n");

        Assert.True(p.IsEmpty);
        Assert.Equal("", UnifiedDiff.Render("c.toml", "same\n", "same\n"));
    }

    [Fact]
    public void DiffModeEndsWithTheEditTotals()
    {
        var text = new Preview().Edit("c.toml", "a\nb\n", "a\nc\nd\n").Render(PreviewMode.Diff);

        Assert.EndsWith(
            "1 file changed, 2 insertions(+), 1 deletion(-)\n",
            text,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void StatModePrintsAlignedStatLinesAndTotals()
    {
        var text = new Preview()
            .Edit("a", "", "1\n2\n3\n")
            .Edit("long/name", "x\n", "")
            .Render(PreviewMode.Stat);

        Assert.Equal(
            " a         | 3 +++\n long/name | 1 -\n2 files changed, 3 insertions(+), 1 deletion(-)\n",
            text
        );
    }

    [Fact]
    public void StatBarsAreCappedAtFortyColumns()
    {
        var big = string.Concat(Enumerable.Range(0, 400).Select(i => $"{i}\n"));
        var text = new Preview()
            .Edit("big", "", big)
            .Edit("small", "a\n", "b\n")
            .Render(PreviewMode.Stat);

        Assert.Contains($"| 400 {new string('+', 40)}\n", text, StringComparison.Ordinal);
        Assert.Contains(" small |   2 +-\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ASmallChangeAloneKeepsItsTrueBar()
    {
        var text = new Preview().Edit("small", "", "a\n").Render(PreviewMode.Stat);

        Assert.StartsWith(" small | 1 +\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BarsPastTheCapRoundUpSoAOneLineChangeStillShows()
    {
        var big = string.Concat(Enumerable.Range(0, 400).Select(i => $"{i}\n"));
        var text = new Preview()
            .Edit("big", "", big)
            .Edit("small", "", "a\n")
            .Render(PreviewMode.Stat);

        Assert.Contains(" small |   1 +\n", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PreviewMode.Diff)]
    [InlineData(PreviewMode.Stat)]
    public void RemovalsPrintSizesFileCountsAndTotalsInBothModes(PreviewMode mode)
    {
        var text = new Preview()
            .Remove("wt/a/target", 13_000_000_000, 48_213)
            .Remove("wt/b/node_modules", 1024, 1)
            .Render(mode);

        Assert.Equal(
            "- wt/a/target        12.1 GB  48 213 files\n"
                + "- wt/b/node_modules   1.0 KB  1 file\n"
                + "2 paths, 48 214 files, -12.1 GB\n",
            text
        );
    }

    [Fact]
    public void AnEmptyPreviewRendersNothing()
    {
        var p = new Preview();

        Assert.True(p.IsEmpty);
        Assert.Equal("", p.Render(PreviewMode.Diff));
        Assert.Equal("", p.Render(PreviewMode.Stat));
    }

    [Fact]
    public void TotalsSumEditsAndRemovals()
    {
        var t = new Preview()
            .Edit("a", "1\n", "2\n3\n")
            .Remove("x", 10, 2)
            .Remove("y", 5, 1)
            .Totals();

        Assert.Equal(new PreviewTotals(1, 2, 1, 2, 3, 15), t);
    }

    [Fact]
    public void JsonHasPathsAndCountsButNoDiffText()
    {
        var json = PreviewJson.Serialize(new Preview().Edit("a", "x\n", "y\n").Remove("t", 10, 2));

        Assert.Equal(
            """{"edits":[{"path":"a","insertions":1,"deletions":1}],"removals":[{"path":"t","bytes":10,"files":2}]}""",
            json
        );
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L * 1024, "1.5 MB")]
    [InlineData(5L << 40, "5.0 TB")]
    public void HumanBytesUseBinaryUnitsWithOneDecimal(long bytes, string expected) =>
        Assert.Equal(expected, PreviewText.HumanBytes(bytes));

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(999L, "999")]
    [InlineData(1000L, "1 000")]
    [InlineData(1_204_311L, "1 204 311")]
    public void GroupSplitsThousandsWithSpaces(long n, string expected) =>
        Assert.Equal(expected, PreviewText.Group(n));

    [Theory]
    [InlineData("")]
    [InlineData("--- a/x\n+y\n")]
    [InlineData("--- a/x\n+y")]
    public void PaintWithoutColourReturnsTheTextUnchanged(string text) =>
        Assert.Same(text, PreviewText.Paint(text, color: false));

    [Fact]
    public void PaintWrapsEachMarkedLineInItsEscape()
    {
        var painted = PreviewText.Paint("--- a/x\n+++ b/x\n@@ -1 +1 @@\n-a\n+b\n c\n", color: true);

        Assert.Equal(
            "\e[1m--- a/x\e[0m\n\e[1m+++ b/x\e[0m\n\e[36m@@ -1 +1 @@\e[39m\n\e[31m-a\e[39m\n\e[32m+b\e[39m\n c\n",
            painted
        );
    }
}
