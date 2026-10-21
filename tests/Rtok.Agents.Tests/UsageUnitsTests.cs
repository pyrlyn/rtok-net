using System.Globalization;
using CsCheck;
using Rtok.Agents.Usage;

namespace Rtok.Agents.Tests;

public sealed class UsageUnitsTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1000, "1K")]
    [InlineData(1080, "1.08K")]
    [InlineData(1125, "1.12K")] // 1.125 is exact: the tie goes to the even digit
    [InlineData(1135, "1.14K")] // 1.135 is stored just above the tie
    [InlineData(2675, "2.67K")] // and 2.675 just below it
    [InlineData(552_890, "552.89K")]
    [InlineData(3_200_000, "3.2M")]
    [InlineData(6_850_000, "6.85M")]
    [InlineData(26_520_000_000, "26.52B")]
    [InlineData(-1500, "-1500")]
    public void Tokens_are_decimal_SI_with_two_decimals(long n, string text) =>
        Assert.Equal(text, UsageUnits.Tokens(n));

    [Theory]
    [InlineData(0.0, "$0.00")]
    [InlineData(7.7, "$7.70")]
    [InlineData(0.004, "$0.00")]
    [InlineData(-0.004, "$0.00")]
    [InlineData(-1.5, "-$1.50")]
    [InlineData(1_234_567.891, "$1,234,567.89")]
    public void Money_is_grouped_dollars_and_cents(double usd, string text) =>
        Assert.Equal(text, UsageUnits.Usd(usd));

    [Fact]
    public void No_price_is_a_dash_not_free() => Assert.Equal("-", UsageUnits.Usd(null));

    [Theory]
    [InlineData(0.125, 2, "0.12")]
    [InlineData(0.375, 2, "0.38")]
    [InlineData(2.675, 2, "2.67")]
    [InlineData(50.5, 0, "50")]
    [InlineData(51.5, 0, "52")]
    [InlineData(0.004, 2, "0.00")]
    [InlineData(12.0, 2, "12.00")]
    public void Fixed_rounds_the_exact_binary_value_half_to_even(
        double v,
        int decimals,
        string text
    ) => Assert.Equal(text, UsageUnits.Fixed(v, decimals));

    [Fact]
    public void A_shown_unit_agrees_with_the_exact_count_to_its_last_digit() =>
        Gen.Long[0, 999_999_999_999_999]
            .Sample(n =>
            {
                var shown = UsageUnits.Tokens(n);
                var scale = shown[^1] switch
                {
                    'B' => 1e9,
                    'M' => 1e6,
                    'K' => 1e3,
                    _ => 1,
                };
                var digits = scale == 1 ? shown : shown[..^1];
                var value = decimal.Parse(digits, CultureInfo.InvariantCulture) * (decimal)scale;
                return Math.Abs(value - n) <= (scale == 1 ? 0 : 0.005m * (decimal)scale);
            });
}
