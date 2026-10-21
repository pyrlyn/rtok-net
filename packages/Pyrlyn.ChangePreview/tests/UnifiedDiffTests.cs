namespace Pyrlyn.ChangePreview.Tests;

public sealed class UnifiedDiffTests
{
    [Fact]
    public void ANewFileStartsAtLineZeroOfTheOldSide() =>
        Assert.Equal(
            "--- a/n\n+++ b/n\n@@ -0,0 +1,2 @@\n+a\n+b\n",
            UnifiedDiff.Render("n", "", "a\nb\n")
        );

    [Fact]
    public void AOneLineRangeHasNoCount() =>
        Assert.Equal(
            "--- a/f\n+++ b/f\n@@ -1 +1 @@\n-a\n+b\n",
            UnifiedDiff.Render("f", "a\n", "b\n")
        );

    [Fact]
    public void AMissingFinalNewlineIsMarked()
    {
        Assert.Equal(
            "--- a/f\n+++ b/f\n@@ -1 +1 @@\n-a\n\\ No newline at end of file\n+a\n",
            UnifiedDiff.Render("f", "a", "a\n")
        );
    }

    [Fact]
    public void ChangesFarApartGetTheirOwnHunksWithThreeLinesOfContext()
    {
        var before = Numbered(20);
        var after = before
            .Replace("\n2\n", "\ntwo\n", StringComparison.Ordinal)
            .Replace("\n19\n", "\nnineteen\n", StringComparison.Ordinal);

        Assert.Equal(
            "--- a/f\n+++ b/f\n"
                + "@@ -1,5 +1,5 @@\n 1\n-2\n+two\n 3\n 4\n 5\n"
                + "@@ -16,5 +16,5 @@\n 16\n 17\n 18\n-19\n+nineteen\n 20\n",
            UnifiedDiff.Render("f", before, after)
        );
    }

    [Fact]
    public void ChangesSixLinesApartShareAHunk()
    {
        var before = Numbered(12);
        var after = before
            .Replace("\n2\n", "\nB\n", StringComparison.Ordinal)
            .Replace("\n9\n", "\nI\n", StringComparison.Ordinal);

        var diff = UnifiedDiff.Render("f", before, after);

        Assert.Single(diff.Split('\n'), l => l.StartsWith("@@", StringComparison.Ordinal));
        Assert.Contains("@@ -1,12 +1,12 @@\n", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletingEverythingEndsAtLineZeroOfTheNewSide() =>
        Assert.Equal(
            "--- a/f\n+++ b/f\n@@ -1,2 +0,0 @@\n-a\n-b\n",
            UnifiedDiff.Render("f", "a\nb\n", "")
        );

    private static string Numbered(int count) =>
        string.Concat(Enumerable.Range(1, count).Select(i => $"{i}\n"));
}
