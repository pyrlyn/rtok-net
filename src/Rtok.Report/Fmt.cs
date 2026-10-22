using System.Globalization;

namespace Rtok.Report;

/// <summary>How the report prints numbers: invariant, integers plain, decimals to one place, and
/// <c>—</c> for a value that is not a number.</summary>
public static class Fmt
{
    /// <summary>What a non-finite value prints.</summary>
    public const string NotANumber = "—";

    /// <summary>An integer.</summary>
    public static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>One decimal place, or <see cref="NotANumber"/>.</summary>
    public static string Dec(double value) =>
        double.IsFinite(value) ? value.ToString("0.0", CultureInfo.InvariantCulture) : NotANumber;

    /// <summary>A percentage of a 0..1 share, one decimal place.</summary>
    public static string Percent(double share) =>
        double.IsFinite(share) ? Dec(share * 100) + "%" : NotANumber;

    /// <summary>A milliseconds value, blank when none was recorded.</summary>
    public static string Ms(double? value) => value is { } v ? Dec(v) : "";

    /// <summary><c>{n} {word}</c>, with an <c>s</c> unless <paramref name="n"/> is 1.</summary>
    public static string Count(long n, string word) => $"{Num(n)} {word}{(n == 1 ? "" : "s")}";
}
