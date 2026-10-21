using System.Globalization;
using System.Text;

namespace Pyrlyn.Durations;

/// <summary>Human durations: <c>30m</c>, <c>24h</c>, <c>7d</c>, <c>1h 30m</c>.</summary>
/// <remarks>
/// <see cref="Parse"/> reads the humantime grammar: one or more <c>&lt;integer&gt;&lt;unit&gt;</c>
/// pairs, optionally separated by spaces, summed. Units: <c>ns</c>, <c>us</c>/<c>µs</c>,
/// <c>ms</c>, <c>s</c>/<c>sec</c>/<c>second</c>, <c>m</c>/<c>min</c>/<c>minute</c>,
/// <c>h</c>/<c>hr</c>/<c>hour</c>, <c>d</c>/<c>day</c>, <c>w</c>/<c>wk</c>/<c>week</c>,
/// <c>M</c>/<c>month</c> (30.44 days) and <c>y</c>/<c>yr</c>/<c>year</c> (365.25 days), with
/// plural forms. There is no sign and no fraction, and a number needs a unit.
/// </remarks>
public static class HumanDuration
{
    private static readonly Dictionary<string, TimeSpan> Units = BuildUnits();

    /// <summary>Parses a human duration.</summary>
    /// <param name="text">The text, for example <c>30m</c>.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="FormatException">The text is empty, has an unknown unit, a sign or a bare number.</exception>
    /// <exception cref="OverflowException">The duration does not fit a <see cref="TimeSpan"/>.</exception>
    public static TimeSpan Parse(string text)
    {
        var error = TryParseCore(text, out var value);
        return error switch
        {
            null => value,
            Overflow => throw new OverflowException($"{text} is out of range"),
            _ => throw new FormatException(error),
        };
    }

    /// <summary>Parses a human duration without throwing.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The duration, or zero.</param>
    /// <returns>True when the text is a duration that fits.</returns>
    public static bool TryParse(string? text, out TimeSpan value) =>
        TryParseCore(text, out value) is null;

    /// <summary>
    /// Parses a window in days or hours: <c>&lt;n&gt;</c> (days), <c>&lt;n&gt;d</c> or
    /// <c>&lt;n&gt;h</c>.
    /// </summary>
    /// <param name="text">The text, for example <c>7</c>, <c>30d</c> or <c>12h</c>.</param>
    /// <returns>The window.</returns>
    /// <exception cref="FormatException">The text is empty, signed or has another unit.</exception>
    /// <exception cref="OverflowException">The window does not fit a <see cref="TimeSpan"/>.</exception>
    public static TimeSpan ParseDaysOrHours(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var s = text.Trim();
        var digits = 0;
        while (digits < s.Length && char.IsAsciiDigit(s[digits]))
        {
            digits++;
        }
        if (digits == 0)
        {
            throw new FormatException($"expected a number of days or hours, got \"{text}\"");
        }
        var unit = s[digits..] switch
        {
            "" or "d" => TimeSpan.FromDays(1),
            "h" => TimeSpan.FromHours(1),
            var u => throw new FormatException($"unknown unit \"{u}\" in \"{text}\"; use d or h"),
        };
        if (
            !ulong.TryParse(
                s.AsSpan(0, digits),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var n
            )
            || n > (ulong)(TimeSpan.MaxValue.Ticks / unit.Ticks)
        )
        {
            throw new OverflowException($"{text} is out of range");
        }
        return TimeSpan.FromTicks((long)n * unit.Ticks);
    }

    /// <summary>
    /// Writes a duration exactly, largest unit first and zero parts left out: <c>0s</c>,
    /// <c>30m</c>, <c>1d</c>, <c>1h30m</c>, <c>250ms</c>. <see cref="Parse"/> reads it back.
    /// </summary>
    /// <param name="value">The duration; negative is written as zero.</param>
    /// <returns>The text.</returns>
    public static string Format(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "0s";
        }
        var sb = new StringBuilder();
        var ticks = value.Ticks;
        foreach (var (unit, size) in FormatUnits)
        {
            if (ticks >= size)
            {
                // The last unit is one tick, which is 100 ns.
                var n = size == 1 ? ticks * 100 : ticks / size;
                sb.Append(CultureInfo.InvariantCulture, $"{n}{unit}");
                ticks %= size;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes a duration in at most two units for narrow columns: <c>45s</c>, <c>5m03s</c>,
    /// <c>2h05m</c>, <c>3d04h</c>. Rounds down to whole seconds; negative is <c>0s</c>.
    /// </summary>
    /// <param name="value">The duration.</param>
    /// <returns>The text.</returns>
    public static string FormatShort(TimeSpan value)
    {
        var secs = Math.Max(0, (long)value.TotalSeconds);
        var inv = CultureInfo.InvariantCulture;
        return secs switch
        {
            < 60 => string.Create(inv, $"{secs}s"),
            < 3_600 => string.Create(inv, $"{secs / 60}m{secs % 60:00}s"),
            < 86_400 => string.Create(inv, $"{secs / 3_600}h{secs % 3_600 / 60:00}m"),
            _ => string.Create(inv, $"{secs / 86_400}d{secs % 86_400 / 3_600:00}h"),
        };
    }

    private const string Overflow = "overflow";

    private static readonly (string Unit, long Ticks)[] FormatUnits =
    [
        ("d", TimeSpan.TicksPerDay),
        ("h", TimeSpan.TicksPerHour),
        ("m", TimeSpan.TicksPerMinute),
        ("s", TimeSpan.TicksPerSecond),
        ("ms", TimeSpan.TicksPerMillisecond),
        ("us", TimeSpan.TicksPerMicrosecond),
        ("ns", 1),
    ];

    /// <summary>Null on success, <see cref="Overflow"/>, or the reason the text is not a duration.</summary>
    private static string? TryParseCore(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        var s = text?.Trim() ?? "";
        if (s.Length == 0)
        {
            return "a duration cannot be empty";
        }
        var i = 0;
        long total = 0;
        while (i < s.Length)
        {
            var start = i;
            while (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                i++;
            }
            if (i == start)
            {
                return $"expected a number at {start} in \"{s}\"";
            }
            var number = s[start..i];
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
            var unitStart = i;
            while (i < s.Length && !char.IsAsciiDigit(s[i]) && !char.IsWhiteSpace(s[i]))
            {
                i++;
            }
            var unit = s[unitStart..i];
            if (unit.Length == 0)
            {
                return $"a unit is needed after {number} in \"{s}\", for example {number}s or {number}m";
            }
            if (!Units.TryGetValue(unit, out var size))
            {
                return $"unknown unit \"{unit}\" in \"{s}\"";
            }
            if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                return Overflow;
            }
            try
            {
                // Nanoseconds are below a tick: whole ticks only.
                total = checked(total + (size.Ticks == 0 ? n / 100 : checked(n * size.Ticks)));
            }
            catch (OverflowException)
            {
                return Overflow;
            }
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }
        value = TimeSpan.FromTicks(total);
        return null;
    }

    private static Dictionary<string, TimeSpan> BuildUnits()
    {
        var units = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        void Add(TimeSpan size, params string[] names)
        {
            foreach (var name in names)
            {
                units[name] = size;
            }
        }
        Add(TimeSpan.Zero, "ns", "nsec", "nanos", "nanosecond", "nanoseconds");
        Add(
            TimeSpan.FromMicroseconds(1),
            "us",
            "µs",
            "usec",
            "micros",
            "microsecond",
            "microseconds"
        );
        Add(TimeSpan.FromMilliseconds(1), "ms", "msec", "millis", "millisecond", "milliseconds");
        Add(TimeSpan.FromSeconds(1), "s", "sec", "secs", "second", "seconds");
        Add(TimeSpan.FromMinutes(1), "m", "min", "mins", "minute", "minutes");
        Add(TimeSpan.FromHours(1), "h", "hr", "hrs", "hour", "hours");
        Add(TimeSpan.FromDays(1), "d", "day", "days");
        Add(TimeSpan.FromDays(7), "w", "wk", "wks", "week", "weeks");
        Add(TimeSpan.FromSeconds(2_630_016), "M", "month", "months");
        Add(TimeSpan.FromSeconds(31_557_600), "y", "yr", "yrs", "year", "years");
        return units;
    }
}
