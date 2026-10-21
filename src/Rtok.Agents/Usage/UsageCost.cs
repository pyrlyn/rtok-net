using Rtok.Config;

namespace Rtok.Agents.Usage;

/// <summary>The price table and the arithmetic every cost estimate shares.</summary>
public static class UsageCost
{
    /// <summary>
    /// The <c>[stats.prices]</c> row for <paramref name="model"/>: the exact id, else without a
    /// provider prefix (<c>anthropic/…</c>), else also without a trailing <c>-YYYYMMDD</c> date.
    /// </summary>
    public static ModelPrice? Price(IDictionary<string, ModelPrice> prices, string model)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(model);
        var bare = model[(model.LastIndexOf('/') + 1)..];
        var dash = bare.LastIndexOf('-');
        var undated =
            dash >= 0 && bare.Length - dash - 1 == 8 && bare[(dash + 1)..].All(char.IsAsciiDigit)
                ? bare[..dash]
                : bare;
        return prices.TryGetValue(model, out var exact) ? exact
            : prices.TryGetValue(bare, out var plain) ? plain
            : prices.TryGetValue(undated, out var dated) ? dated
            : null;
    }

    /// <summary>The estimate of one request's legs at <paramref name="price"/> (USD per million tokens).</summary>
    public static double RowCost(
        long input,
        long cacheWrite,
        long cacheRead,
        long output,
        ModelPrice price
    )
    {
        ArgumentNullException.ThrowIfNull(price);
        static double Leg(long n, double rate) => n / 1e6 * rate;
        return Round6(
            Leg(input, price.Input)
                + Leg(cacheWrite, price.CacheWrite)
                + Leg(cacheRead, price.CacheRead)
                + Leg(output, price.Output)
        );
    }

    /// <summary>Rounded to a millionth of a dollar, halves away from zero.</summary>
    public static double Round6(double x) =>
        Math.Round(x * 1e6, MidpointRounding.AwayFromZero) / 1e6;
}
