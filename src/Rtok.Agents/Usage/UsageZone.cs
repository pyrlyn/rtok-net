using System.Globalization;
using Pyrlyn.Durations;
using Rtok.Config;

namespace Rtok.Agents.Usage;

/// <summary>The zone the report's days and months are in, and the window bounds read in it.</summary>
internal static class UsageZone
{
    /// <summary><paramref name="tz"/> as an IANA zone; empty is the system zone.</summary>
    /// <exception cref="ConfigException">The zone is unknown.</exception>
    public static TimeZoneInfo Find(string tz)
    {
        if (tz.Length == 0)
        {
            return TimeZoneInfo.Local;
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tz);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ConfigException(
                $"agents.usage.tz `{tz}`: expected an IANA zone such as Europe/Kyiv",
                e
            );
        }
    }

    /// <summary>The zone's IANA id, or <c>local</c> for a system zone that has none.</summary>
    public static string Name(TimeZoneInfo zone) => zone.HasIanaId ? zone.Id : "local";

    /// <summary>
    /// Midnight starting <paramref name="day"/> in <paramref name="zone"/>, in unix seconds. A
    /// midnight that a DST change skips or repeats is read at the offset in force before the
    /// change: the first instant of the day either way.
    /// </summary>
    public static long StartOf(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var offset =
            zone.IsInvalidTime(local) ? zone.GetUtcOffset(local.AddDays(-1))
            : zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUnixTimeSeconds();
    }

    /// <summary>The day <paramref name="ts"/> (unix seconds) falls on in <paramref name="zone"/>, as <c>yyyy-MM-dd</c>.</summary>
    public static string DayOf(long ts, TimeZoneInfo zone) =>
        TimeZoneInfo
            .ConvertTime(DateTimeOffset.FromUnixTimeSeconds(ts), zone)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// The window's start: empty is unbounded (0), a date is that day's midnight, anything else
    /// a span back from <paramref name="now"/> (<c>30d</c>, <c>12h</c>, a bare number of days).
    /// </summary>
    /// <exception cref="ConfigException">The value is neither.</exception>
    public static long Since(string value, TimeZoneInfo zone, long now)
    {
        if (value.Length == 0)
        {
            return 0;
        }
        if (Date(value) is { } day)
        {
            return StartOf(day, zone);
        }
        try
        {
            return now - (long)HumanDuration.ParseDaysOrHours(value).TotalSeconds;
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            throw new ConfigException(
                $"agents.usage.since `{value}`: expected a date or a duration such as 30d",
                e
            );
        }
    }

    /// <summary>The window's end: empty is unbounded, a date ends with that day.</summary>
    /// <exception cref="ConfigException">The value is not a date.</exception>
    public static long Until(string value, TimeZoneInfo zone)
    {
        if (value.Length == 0)
        {
            return long.MaxValue;
        }
        return Date(value) is { } day && day < DateOnly.MaxValue
            ? StartOf(day.AddDays(1), zone)
            : throw new ConfigException(
                $"agents.usage.until `{value}`: expected a date such as 2026-09-30"
            );
    }

    private static DateOnly? Date(string value) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var day
        )
            ? day
            : null;
}
