namespace Pyrlyn.Ignore.Tests;

public sealed class IgnorePatternTests
{
    [Theory]
    // No slash: the basename at any depth.
    [InlineData("*.log", "a.log", false, true)]
    [InlineData("*.log", "x/y/a.log", false, true)]
    [InlineData("frotz", "a/doc/frotz", false, true)]
    [InlineData("*", "a/b", false, true)]
    [InlineData("foo*", "foo/bar", false, false)]
    [InlineData("a**b", "axxb", false, true)]
    [InlineData("a**b", "a/b", false, false)]
    // A slash at the start or in the middle anchors to the file's directory.
    [InlineData("/a.txt", "a.txt", false, true)]
    [InlineData("/a.txt", "x/a.txt", false, false)]
    [InlineData("doc/frotz", "doc/frotz", false, true)]
    [InlineData("doc/frotz", "a/doc/frotz", false, false)]
    [InlineData("/*.c", "a.c", false, true)]
    [InlineData("/*.c", "x/a.c", false, false)]
    [InlineData("a/b*", "a/bc", false, true)]
    [InlineData("doc/*.tmp", "doc/sub/x.tmp", false, false)]
    // A trailing slash names directories only.
    [InlineData("foo/", "foo", false, false)]
    [InlineData("foo/", "foo", true, true)]
    [InlineData("foo/", "x/foo", true, true)]
    [InlineData("x/foo/", "x/foo", true, true)]
    // Leading, trailing and middle **.
    [InlineData("**/foo", "foo", false, true)]
    [InlineData("**/foo", "a/b/foo", false, true)]
    [InlineData("**/foo/bar", "x/y/foo/bar", false, true)]
    [InlineData("abc/**", "abc/x/y", false, true)]
    [InlineData("abc/**", "abc", true, false)]
    [InlineData("a/**/b", "a/b", false, true)]
    [InlineData("a/**/b", "a/x/y/b", false, true)]
    [InlineData("a/**/b", "a/xb", false, false)]
    [InlineData("a/**b", "a/x/b", false, false)]
    [InlineData("**", "a/b/c", false, true)]
    // ?, brackets and classes never match a slash.
    [InlineData("?.c", "a.c", false, true)]
    [InlineData("?.c", "ab.c", false, false)]
    [InlineData("a?b", "a/b", false, false)]
    [InlineData("[ab].c", "a.c", false, true)]
    [InlineData("[!ab].c", "c.c", false, true)]
    [InlineData("[!ab].c", "a.c", false, false)]
    [InlineData("[^a].c", "b.c", false, true)]
    [InlineData("a[!x]b", "a/b", false, false)]
    [InlineData("[a-c]x", "bx", false, true)]
    [InlineData("[a-c]x", "dx", false, false)]
    [InlineData("[c-a]x", "bx", false, false)]
    [InlineData("[]]x", "]x", false, true)]
    [InlineData("[!]]x", "ax", false, true)]
    [InlineData("[[:digit:]]x", "5x", false, true)]
    [InlineData("[[:upper:]]", "a", false, false)]
    [InlineData("[a\\]]", "]", false, true)]
    [InlineData("x[", "x[", false, false)]
    [InlineData("x[[:nope:]]", "xa", false, false)]
    // Escapes.
    [InlineData("\\#a", "#a", false, true)]
    [InlineData("\\!a", "!a", false, true)]
    [InlineData("a\\*", "a*", false, true)]
    [InlineData("a\\*", "ab", false, false)]
    [InlineData("a\\", "a\\", false, false)]
    [InlineData("a.b", "aXb", false, false)]
    [InlineData("(a)+", "(a)+", false, true)]
    public void MatchesLikeGit(string line, string path, bool isDir, bool expected)
    {
        var pattern = IgnorePattern.Parse(line);
        Assert.NotNull(pattern);
        Assert.Equal(expected, pattern.IsMatch(path, isDir));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# comment")]
    [InlineData("#")]
    [InlineData("\r")]
    public void BlankLinesAndCommentsAreNoRule(string line) =>
        Assert.Null(IgnorePattern.Parse(line));

    [Fact]
    public void NegationAndTrailingSpaces()
    {
        var negated = IgnorePattern.Parse("!keep.log")!;
        Assert.True(negated.Negated);
        Assert.True(negated.IsMatch("keep.log", false));

        Assert.True(IgnorePattern.Parse("a.txt  \r")!.IsMatch("a.txt", false));
        Assert.True(IgnorePattern.Parse("a\\ ")!.IsMatch("a ", false));
        Assert.False(IgnorePattern.Parse("a\\ ")!.IsMatch("a", false));
        Assert.True(IgnorePattern.Parse("a\\  ")!.IsMatch("a ", false));
    }

    [Fact]
    public void IgnoreCaseFoldsLiteralsSuffixesAndGlobs()
    {
        Assert.True(IgnorePattern.Parse("README", ignoreCase: true)!.IsMatch("readme", false));
        Assert.True(IgnorePattern.Parse("*.TXT", ignoreCase: true)!.IsMatch("a.txt", false));
        Assert.True(IgnorePattern.Parse("a?C", ignoreCase: true)!.IsMatch("abc", false));
        Assert.False(IgnorePattern.Parse("*.TXT")!.IsMatch("a.txt", false));
    }

    [Fact]
    public void TheLastRuleNamingAPathDecides()
    {
        var file = IgnoreFile.Parse("/repo", "*.log\n!keep.log\n# x\nsub/\n");
        Assert.Equal(IgnoreMatch.Ignore, file.Match("/repo/a.log", false));
        Assert.Equal(IgnoreMatch.Whitelist, file.Match("/repo/x/keep.log", false));
        Assert.Equal(IgnoreMatch.Ignore, file.Match("/repo/sub", true));
        Assert.Equal(IgnoreMatch.None, file.Match("/repo/a.txt", false));
        Assert.Equal(IgnoreMatch.None, file.Match("/elsewhere/a.log", false));
        Assert.Equal(IgnoreMatch.Ignore, file.Match("x/a.log", false));
    }

    [Fact]
    public void ABomIsSkipped() =>
        Assert.Equal(IgnoreMatch.Ignore, IgnoreFile.Parse("/r", "﻿*.log").Match("/r/a.log", false));
}
