using System.Globalization;
using System.Numerics;

namespace Rtok.Agents.Usage;

/// <summary>How the report writes counts, money and shares.</summary>
internal static class UsageUnits
{
    /// <summary><c>22.1K</c>, <c>155.45M</c>, <c>26.52B</c>: decimal SI, two decimals, trailing zeros dropped.</summary>
    public static string Tokens(long n)
    {
        var (scale, suffix) = n switch
        {
            >= 1_000_000_000 => (1e9, "B"),
            >= 1_000_000 => (1e6, "M"),
            >= 1_000 => (1e3, "K"),
            _ => (0d, ""),
        };
        if (suffix.Length == 0)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
        return Fixed(n / scale, 2).TrimEnd('0').TrimEnd('.') + suffix;
    }

    /// <summary><c>1,234</c>.</summary>
    public static string Grouped(long n) => n.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary><c>$1,234.56</c>; <c>-</c> when nothing had a price. A negative estimate keeps its sign.</summary>
    public static string Usd(double? cost)
    {
        if (cost is not { } c)
        {
            return "-";
        }
        var cents = (long)Math.Round(Math.Abs(c) * 100, MidpointRounding.AwayFromZero);
        var sign = c < 0 && cents > 0 ? "-" : "";
        return $"{sign}${Grouped(cents / 100)}.{cents % 100:00}";
    }

    /// <summary>
    /// <paramref name="v"/> with <paramref name="decimals"/> digits after the point, rounded from
    /// its exact binary value with ties to even: <c>1.125</c> is <c>1.12</c>, <c>1.135</c> (stored
    /// just below) is <c>1.13</c>.
    /// </summary>
    public static string Fixed(double v, int decimals)
    {
        if (!double.IsFinite(v) || v < 0)
        {
            return v.ToString(
                "F" + decimals.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture
            );
        }
        var bits = BitConverter.DoubleToInt64Bits(v);
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xF_FFFF_FFFF_FFFFL;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantissa |= 1L << 52;
        }
        exponent -= 1075;
        // v = mantissa * 2^exponent exactly; scaled = v * 10^decimals = num / den.
        var num = mantissa * BigInteger.Pow(10, decimals);
        var den = BigInteger.One;
        if (exponent >= 0)
        {
            num <<= exponent;
        }
        else
        {
            den <<= -exponent;
        }
        var q = BigInteger.DivRem(num, den, out var r);
        var twice = r * 2;
        if (twice > den || (twice == den && !q.IsEven))
        {
            q += 1;
        }
        var digits = q.ToString(CultureInfo.InvariantCulture).PadLeft(decimals + 1, '0');
        return decimals == 0 ? digits : $"{digits[..^decimals]}.{digits[^decimals..]}";
    }
}
