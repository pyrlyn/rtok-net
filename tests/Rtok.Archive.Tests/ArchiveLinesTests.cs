namespace Rtok.Archive.Tests;

/// <summary>The line views: splitting, <c>--lines</c>, <c>--grep</c>, <c>--context</c> and the
/// <c>max_lines</c> trailer.</summary>
public sealed class ArchiveLinesTests
{
    private static readonly List<string> Ten =
    [
        .. Enumerable.Range(1, 10).Select(i => $"line {i}"),
    ];

    [Theory]
    [InlineData("", new string[0])]
    [InlineData("a", new[] { "a" })]
    [InlineData("a\n", new[] { "a" })]
    [InlineData("\n", new[] { "" })]
    [InlineData("a\r\nb\r\n", new[] { "a", "b" })]
    [InlineData("a\n\nb", new[] { "a", "", "b" })]
    [InlineData("a\rb\r", new[] { "a\rb\r" })]
    public void Lines_split_at_newlines_and_drop_the_cr_before_one(string text, string[] lines) =>
        Assert.Equal(lines, ArchiveLines.Split(text), StringComparer.Ordinal);

    [Fact]
    public void A_range_keeps_its_lines() =>
        Assert.Equal(["line 3", "line 4", "line 5"], ArchiveLines.Filter(Ten, new(3, 5), null, 0));

    [Fact]
    public void Grep_hits_are_numbered_by_their_place_in_the_body() =>
        Assert.Equal(["1:line 1", "10:line 10"], ArchiveLines.Filter(Ten, null, "line 1", 0));

    [Fact]
    public void Grep_inside_a_range_keeps_absolute_numbers() =>
        Assert.Equal(["7:line 7"], ArchiveLines.Filter(Ten, new(6, 8), "7", 0));

    [Fact]
    public void A_pattern_that_does_not_compile_matches_literally()
    {
        List<string> lines = ["a(b", "ab", "(x"];
        Assert.Equal(["1:a(b"], ArchiveLines.Filter(lines, null, "a(b", 0));
    }

    [Fact]
    public void Context_merges_touching_windows_and_separates_blocks()
    {
        Assert.Equal(
            [
                "1:line 1",
                "2:line 2",
                "3:line 3",
                "4:line 4",
                "--",
                "8:line 8",
                "9:line 9",
                "10:line 10",
            ],
            ArchiveLines.Filter(Ten, null, "^line (2|3|9)$", 1)
        );
        Assert.Equal(
            ["1:line 1", "2:line 2", "--", "9:line 9", "10:line 10"],
            ArchiveLines.Filter(Ten, null, "^line (1|10)$", 1)
        );
    }

    [Fact]
    public void A_huge_context_clamps_to_the_range() =>
        Assert.Equal(
            ["4:line 4", "5:line 5", "6:line 6"],
            ArchiveLines.Filter(Ten, new(4, 6), "5", int.MaxValue)
        );

    [Fact]
    public void Context_without_grep_is_ignored() =>
        Assert.Equal(Ten, ArchiveLines.Filter(Ten, null, null, 3));

    [Fact]
    public void Max_lines_cuts_and_names_the_short_id()
    {
        var id = new string('a', 64);
        Assert.Equal(
            "line 1\nline 2\n… 8 lines omitted (expand aaaaaaaa)",
            ArchiveLines.Render(Ten, id, null, null, 0, 2)
        );
        Assert.Equal(string.Join('\n', Ten), ArchiveLines.Render(Ten, id, null, null, 0, 0));
    }
}
