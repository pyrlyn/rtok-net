using System.Globalization;
using Rtok.Agents.Usage;
using Rtok.Config;

namespace Rtok.Agents.Tests;

public sealed class UsageZoneTests
{
    private const long Now = 1_800_000_000;

    [Fact]
    public void A_midnight_that_DST_skips_starts_the_day_at_the_first_real_instant()
    {
        // Chile moves its clocks from 00:00 to 01:00 on the first Sunday of September.
        var zone = UsageFixture.Zone("America/Santiago");
        var midnight = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.SkipWhen(
            zone is null || !zone.IsInvalidTime(midnight),
            "no DST gap at this midnight here"
        );

        var start = UsageZone.StartOf(DateOnly.FromDateTime(midnight), zone);

        Assert.Equal(
            DateTimeOffset
                .Parse("2026-09-06T04:00:00Z", CultureInfo.InvariantCulture)
                .ToUnixTimeSeconds(),
            start
        );
        Assert.Equal("2026-09-06", UsageZone.DayOf(start, zone));
        Assert.Equal("2026-09-05", UsageZone.DayOf(start - 1, zone));
    }

    [Fact]
    public void A_midnight_that_DST_repeats_starts_the_day_at_its_first_pass()
    {
        // Cuba moves its clocks from 01:00 back to 00:00 on the first Sunday of November.
        var zone = UsageFixture.Zone("America/Havana");
        var midnight = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.SkipWhen(
            zone is null || !zone.IsAmbiguousTime(midnight),
            "no repeated midnight here"
        );

        var start = UsageZone.StartOf(DateOnly.FromDateTime(midnight), zone);

        Assert.Equal(
            DateTimeOffset
                .Parse("2026-11-01T04:00:00Z", CultureInfo.InvariantCulture)
                .ToUnixTimeSeconds(),
            start
        );
        Assert.Equal("2026-10-31", UsageZone.DayOf(start - 1, zone));
    }

    [Fact]
    public void Days_follow_the_offset_across_the_autumn_change()
    {
        // Kyiv leaves +03 for +02 at 04:00 on 2026-10-25.
        var zone = UsageFixture.Zone("Europe/Kyiv");
        Assert.SkipWhen(zone is null, "no IANA zones here");

        Assert.Equal("2026-10-25", UsageZone.DayOf(Unix("2026-10-24T21:00:00Z"), zone));
        Assert.Equal("2026-10-24", UsageZone.DayOf(Unix("2026-10-24T20:59:59Z"), zone));
        Assert.Equal("2026-10-25", UsageZone.DayOf(Unix("2026-10-25T21:59:59Z"), zone));
        Assert.Equal("2026-10-26", UsageZone.DayOf(Unix("2026-10-25T22:00:00Z"), zone));
        Assert.Equal(Unix("2026-10-25T22:00:00Z"), UsageZone.Until("2026-10-25", zone));
        Assert.Equal(Unix("2026-10-24T21:00:00Z"), UsageZone.Since("2026-10-25", zone, Now));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("30d", Now - (30 * 86_400))]
    [InlineData("12h", Now - (12 * 3_600))]
    [InlineData("7", Now - (7 * 86_400))]
    public void Since_is_a_date_or_a_span_back_from_now(string value, long since) =>
        Assert.Equal(since, UsageZone.Since(value, TimeZoneInfo.Utc, Now));

    [Fact]
    public void An_empty_until_is_unbounded() =>
        Assert.Equal(long.MaxValue, UsageZone.Until("", TimeZoneInfo.Utc));

    [Theory]
    [InlineData("7x")]
    [InlineData("-1d")]
    [InlineData("2026-13-01")]
    public void A_bad_since_is_named(string value)
    {
        var e = Assert.Throws<ConfigException>(() => UsageZone.Since(value, TimeZoneInfo.Utc, Now));
        Assert.Equal(
            $"agents.usage.since `{value}`: expected a date or a duration such as 30d",
            e.Message
        );
    }

    [Fact]
    public void The_zone_name_is_its_IANA_id()
    {
        Assert.Equal("UTC", UsageZone.Name(UsageZone.Find("UTC")));
        Assert.Equal(
            "agents.usage.tz `Mars/Olympus`: expected an IANA zone such as Europe/Kyiv",
            Assert.Throws<ConfigException>(() => UsageZone.Find("Mars/Olympus")).Message
        );
    }

    private static long Unix(string instant) =>
        DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture).ToUnixTimeSeconds();
}
