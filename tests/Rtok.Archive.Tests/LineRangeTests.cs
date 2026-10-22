using CsCheck;

namespace Rtok.Archive.Tests;

/// <summary><c>--lines</c>: rtok's grammar plus the 25 spellings agents sent in one week
/// (rtok T522).</summary>
public sealed class LineRangeTests
{
    [Theory]
    [InlineData("320 - 360", 320, 360)]
    [InlineData("36<br/>-60", 36, 60)]
    [InlineData("10:45", 10, 45)]
    [InlineData("560 650", 560, 650)]
    [InlineData("[1, 50]", 1, 50)]
    [InlineData("[200, 260]", 200, 260)]
    [InlineData("[242, 300]", 242, 300)]
    [InlineData("[317, 390]", 317, 390)]
    [InlineData("12 - 100", 12, 100)]
    [InlineData("200 - 380", 200, 380)]
    [InlineData("580:650", 580, 650)]
    [InlineData("1 40", 1, 40)]
    [InlineData("1860_1960", 1860, 1960)]
    [InlineData("283 350", 283, 350)]
    [InlineData("136\"-\"215", 136, 215)]
    [InlineData("12 - 70", 12, 70)]
    [InlineData("215\"-\"460", 215, 460)]
    [InlineData("235\"-\"270", 235, 270)]
    [InlineData("410<br>", 410, 410)]
    [InlineData("420 - 640", 420, 640)]
    [InlineData("490<br>-800", 490, 800)]
    [InlineData("52<br>", 52, 52)]
    [InlineData("60<br>", 60, 60)]
    [InlineData("610 614", 610, 614)]
    [InlineData("95<br>", 95, 95)]
    public void The_logged_spellings_parse_to_their_two_lines(string spec, long a, long b)
    {
        Assert.True(LineRange.TryParse(spec, long.MaxValue, out var range, out var error), error);
        Assert.Equal(new LineRange(a, b), range);
    }

    [Theory]
    [InlineData("3-5", 3, 5)]
    [InlineData("3", 3, 10)]
    [InlineData("-4", 1, 4)]
    [InlineData("7-", 7, 10)]
    [InlineData("-", 1, 10)]
    [InlineData("\"2-3\"", 2, 3)]
    [InlineData("'2-3'", 2, 3)]
    [InlineData("4, 6", 4, 6)]
    [InlineData("8-99", 8, 10)]
    public void Rtok_grammar_still_holds(string spec, long a, long b)
    {
        Assert.True(LineRange.TryParse(spec, 10, out var range, out var error), error);
        Assert.Equal(new LineRange(a, b), range);
    }

    [Theory]
    [InlineData("", "expected a positive line or a-b")]
    [InlineData("  ", "expected a positive line or a-b")]
    [InlineData("abc", "expected a positive line or a-b")]
    [InlineData("x-5", "expected a positive line or a-b")]
    [InlineData("1.5-2", "expected a positive line or a-b")]
    [InlineData("1 2 3", "expected a positive line or a-b")]
    [InlineData("80-92,826-850", "expected a positive line or a-b")]
    [InlineData("190·225", "expected a positive line or a-b")]
    [InlineData("1140\">1200", "expected a positive line or a-b")]
    [InlineData("1120in-1200", "expected a positive line or a-b")]
    [InlineData("<br>", "expected a positive line or a-b")]
    [InlineData("99999999999999999999", "expected a positive line or a-b")]
    [InlineData("0", "lines are 1-based")]
    [InlineData("0-5", "lines are 1-based")]
    [InlineData("5-3", "start exceeds end")]
    [InlineData("11-12", "start exceeds line count 10")]
    public void Non_ranges_fail_with_invalid_line_range(string spec, string reason)
    {
        Assert.False(LineRange.TryParse(spec, 10, out _, out var error));
        Assert.StartsWith("invalid line range `", error, StringComparison.Ordinal);
        Assert.EndsWith(reason, error, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_body_rejects_every_range()
    {
        foreach (var spec in (string[])["1", "1-1", "-", "-3", "2-9"])
        {
            Assert.False(LineRange.TryParse(spec, 0, out _, out var error));
            Assert.Contains("start exceeds line count 0", error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Dash_colon_and_spaced_dash_parse_the_same() =>
        Gen.Long[1, 1_000_000]
            .Select(Gen.Long[0, 1_000_000], (a, d) => (A: a, B: a + d))
            .Sample(p =>
            {
                foreach (var spec in (string[])[$"{p.A}:{p.B}", $"{p.A} - {p.B}", $"{p.A},{p.B}"])
                {
                    Assert.True(LineRange.TryParse(spec, long.MaxValue, out var range, out _));
                    Assert.Equal(new LineRange(p.A, p.B), range);
                }
                Assert.True(LineRange.TryParse($"{p.A}-{p.B}", long.MaxValue, out var dash, out _));
                Assert.Equal(new LineRange(p.A, p.B), dash);
            });
}
