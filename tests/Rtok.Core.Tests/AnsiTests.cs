namespace Rtok.Core.Tests;

public sealed class AnsiTests
{
    [Theory]
    [InlineData("\u001b[1;32mok", 7)]
    [InlineData("\u001b]0;title\u0007ok", 10)]
    [InlineData("\u001b]8;;x\u001b\\ok", 8)]
    [InlineData("\u001b(Bok", 2)]
    [InlineData("\u001b[12", 4)]
    [InlineData("\u001b", 1)]
    [InlineData("\u001bé", 1)]
    public void Skip_escape_returns_the_index_after_it(string s, int next) =>
        Assert.Equal(next, Ansi.SkipEscape(s, 0));
}
