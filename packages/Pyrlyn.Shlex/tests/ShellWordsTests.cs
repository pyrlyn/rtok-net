namespace Pyrlyn.Shlex.Tests;

public sealed class ShellWordsTests
{
    // The split table of the shlex crate that rtok used, plus cases from rtok's guard and doctor.
    public static TheoryData<string, string[]?> SplitCases =>
        new()
        {
            { "foo$baz", ["foo$baz"] },
            { "foo baz", ["foo", "baz"] },
            { "foo\"bar\"baz", ["foobarbaz"] },
            { "foo \"bar\"baz", ["foo", "barbaz"] },
            { "   foo \nbar", ["foo", "bar"] },
            { "foo\\\nbar", ["foobar"] },
            { "\"foo\\\nbar\"", ["foobar"] },
            { "'baz\\$b'", ["baz\\$b"] },
            { "'baz\\''", null },
            { "\\", null },
            { "\"\\", null },
            { "'\\", null },
            { "\"", null },
            { "'", null },
            { "foo #bar\nbaz", ["foo", "baz"] },
            { "foo #bar", ["foo"] },
            { "foo#bar", ["foo#bar"] },
            { "foo\"#bar", null },
            { "'\\n'", ["\\n"] },
            { "'\\\\n'", ["\\\\n"] },
            { "", [] },
            { "  \t ", [] },
            { "''", [""] },
            { "a '' b", ["a", "", "b"] },
            { "\"a\\b\\\"c\"", ["a\\b\"c"] },
            { "sed -n '1,5p' file.txt", ["sed", "-n", "1,5p", "file.txt"] },
            { "git log --format=\"%h %s\"", ["git", "log", "--format=%h %s"] },
            { "grep -r 'a|b' src | head", ["grep", "-r", "a|b", "src", "|", "head"] },
            { "node \"$HOME/x.js\"", ["node", "$HOME/x.js"] },
        };

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void SplitsLikePosixShell(string text, string[]? expected)
    {
        Assert.Equal(expected is not null, ShellWords.TrySplit(text, out var words));
        Assert.Equal(expected ?? [], words);
    }

    [Theory]
    [InlineData("echo 'open", "unterminated single quote at 5")]
    [InlineData("echo \"open", "unterminated double quote at 5")]
    [InlineData("echo \\", "trailing backslash at 5")]
    public void ReportsWhereItFailed(string text, string message)
    {
        Assert.False(ShellWords.TrySplit(text, out _, out var error));
        Assert.Equal(message, error);
        Assert.Equal(message, Assert.Throws<FormatException>(() => ShellWords.Split(text)).Message);
    }

    [Theory]
    [InlineData("", "''")]
    [InlineData("plain-word_1.txt", "plain-word_1.txt")]
    [InlineData("a b", "'a b'")]
    [InlineData("can't", "'can'\"'\"'t'")]
    [InlineData("$HOME", "'$HOME'")]
    public void QuotesOnlyWhenNeeded(string word, string quoted)
    {
        Assert.Equal(quoted, ShellWords.Quote(word));
    }

    [Fact]
    public void JoinRoundTrips()
    {
        string[] words = ["echo", "a b", "", "it's", "\\n", "\"q\"", "#not comment", "x\ny"];
        Assert.Equal(words, ShellWords.Split(ShellWords.Join(words)));
    }
}
