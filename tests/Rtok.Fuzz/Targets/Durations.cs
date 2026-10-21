using Pyrlyn.Durations;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// <see cref="HumanDuration"/> on any text: parsing fails only with its documented exceptions,
/// agrees with <c>TryParse</c>, and <c>Format</c> reads back exactly.
/// </summary>
internal static class Durations
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var text = FuzzInput.Text(data);
        var ok = HumanDuration.TryParse(text, out var value);
        try
        {
            var parsed = HumanDuration.Parse(text);
            FuzzInput.Check(ok && parsed == value, "Parse accepted what TryParse refused");
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            FuzzInput.Check(!ok, $"Parse threw {e.GetType().Name} where TryParse succeeded");
        }
        if (ok)
        {
            FuzzInput.Check(value >= TimeSpan.Zero, "negative duration");
            FuzzInput.Check(
                HumanDuration.Parse(HumanDuration.Format(value)) == value,
                "Format does not read back"
            );
            _ = HumanDuration.FormatShort(value);
        }
        try
        {
            FuzzInput.Check(
                HumanDuration.ParseDaysOrHours(text) >= TimeSpan.Zero,
                "negative window"
            );
        }
        catch (Exception e) when (e is FormatException or OverflowException) { }
    }
}
