namespace Pyrlyn.Durations.Tests;

public sealed class HumanDurationTests
{
    // rtok's `[agents] idle`, `--idle` and `--stale-lock` cases, plus the rest of the grammar.
    [Theory]
    [InlineData("0s", 0)]
    [InlineData("1s", 1)]
    [InlineData("1m", 60)]
    [InlineData("10m", 600)]
    [InlineData("15m", 900)]
    [InlineData("30m", 1_800)]
    [InlineData("24h", 86_400)]
    [InlineData("7d", 604_800)]
    [InlineData("1h 30m", 5_400)]
    [InlineData("1h30m", 5_400)]
    [InlineData(" 2 days ", 172_800)]
    [InlineData("1w", 604_800)]
    [InlineData("1M", 2_630_016)]
    [InlineData("1y", 31_557_600)]
    public void ParsesHumanDurations(string text, long seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), HumanDuration.Parse(text));
    }

    [Fact]
    public void ParsesSubSecondUnits()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(250), HumanDuration.Parse("250ms"));
        Assert.Equal(TimeSpan.FromMicroseconds(3), HumanDuration.Parse("3us"));
        Assert.Equal(TimeSpan.FromTicks(5), HumanDuration.Parse("500ns"));
    }

    [Theory]
    [InlineData("5x")]
    [InlineData("-1d")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("5")]
    [InlineData("1.5h")]
    [InlineData("h")]
    [InlineData("1d -1h")]
    public void RejectsWhatIsNotADuration(string text)
    {
        Assert.False(HumanDuration.TryParse(text, out _));
        Assert.Throws<FormatException>(() => HumanDuration.Parse(text));
    }

    [Fact]
    public void ReportsOverflow()
    {
        Assert.Throws<OverflowException>(() => HumanDuration.Parse("99999999999999999999d"));
        Assert.Throws<OverflowException>(() => HumanDuration.Parse("99999999999999y"));
        Assert.False(HumanDuration.TryParse("99999999999999y", out _));
    }

    [Theory]
    [InlineData("7", 7 * 86_400)]
    [InlineData("30d", 30 * 86_400)]
    [InlineData("12h", 12 * 3_600)]
    [InlineData(" 0d ", 0)]
    public void ParsesDayOrHourWindows(string text, long seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), HumanDuration.ParseDaysOrHours(text));
    }

    [Theory]
    [InlineData("5x")]
    [InlineData("-1d")]
    [InlineData("")]
    [InlineData("30m")]
    [InlineData("d")]
    public void RejectsBadWindows(string text)
    {
        Assert.Throws<FormatException>(() => HumanDuration.ParseDaysOrHours(text));
    }

    [Fact]
    public void ReportsWindowOverflow()
    {
        Assert.Throws<OverflowException>(() =>
            HumanDuration.ParseDaysOrHours("99999999999999999d")
        );
    }

    [Theory]
    [InlineData("0s", "0s")]
    [InlineData("30m", "30m")]
    [InlineData("24h", "1d")]
    [InlineData("7d", "7d")]
    [InlineData("1h 30m", "1h30m")]
    [InlineData("90m", "1h30m")]
    [InlineData("1d 1s 250ms", "1d1s250ms")]
    public void FormatsExactlyAndRoundTrips(string text, string formatted)
    {
        var value = HumanDuration.Parse(text);
        Assert.Equal(formatted, HumanDuration.Format(value));
        Assert.Equal(value, HumanDuration.Parse(formatted));
    }

    // rtok's session-duration column.
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(45, "45s")]
    [InlineData(65, "1m05s")]
    [InlineData(3_661, "1h01m")]
    [InlineData(90_000, "1d01h")]
    [InlineData(-5, "0s")]
    public void FormatsShortWithTwoUnitsAtMost(long seconds, string formatted)
    {
        Assert.Equal(formatted, HumanDuration.FormatShort(TimeSpan.FromSeconds(seconds)));
    }
}
