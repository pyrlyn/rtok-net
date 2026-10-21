namespace Rtok.Core.Tests;

public sealed class ShellHopsTests
{
    [Theory]
    [InlineData("a b", "b")]
    [InlineData("a", "")]
    [InlineData("~/'My Documents' && ls", "&& ls")]
    [InlineData("\"a && b\"\t&& x", "&& x")]
    [InlineData("'open", null)]
    [InlineData("a\"b", null)]
    public void Skip_word_is_quote_aware(string s, string? rest) =>
        Assert.Equal(rest, ShellHops.SkipWord(s));

    [Theory]
    [InlineData("cd crates/rtok && cargo test", "crates/rtok", "cargo test")]
    [InlineData("cd \"a && b\" && cd c && git status", "\"a && b\"", "cd c && git status")]
    [InlineData("cd ~/'My Documents' &&ls", "~/'My Documents'", "ls")]
    public void Strip_cd_hop_splits_at_top_level(string s, string target, string rest)
    {
        Assert.True(ShellHops.StripCdHop(s, out var t, out var r));
        Assert.Equal(target, t);
        Assert.Equal(rest, r);
    }

    [Theory]
    [InlineData("cd /repo")]
    [InlineData("cd /repo; ls")]
    [InlineData("cd 'open && ls")]
    [InlineData("ls && cd x")]
    [InlineData("cdx && ls")]
    public void Strip_cd_hop_refuses_anything_else(string s) =>
        Assert.False(ShellHops.StripCdHop(s, out _, out _));
}
