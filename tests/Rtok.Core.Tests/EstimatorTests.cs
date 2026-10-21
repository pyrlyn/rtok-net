using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Core.Tests;

public sealed class EstimatorTests
{
    [Theory]
    [InlineData("fn main() { println!(\"hi there\"); }", TextClass.Code, 10)]
    [InlineData("The quick brown fox jumps over the lazy dog", TextClass.Prose, 11)]
    [InlineData("{\"a\":1,\"b\":[1,2,3],\"c\":\"xyz\"}", TextClass.Json, 10)]
    [InlineData("東京都渋谷区", TextClass.Cjk, 6)]
    [InlineData("", TextClass.Prose, 0)]
    public void Default_rates_match_rtok(string text, TextClass textClass, int tokens) =>
        Assert.Equal(tokens, TokenEstimator.Estimate(text, textClass, EstimatorRates.Default));

    [Fact]
    public void Default_rates_are_the_documented_ones() =>
        Assert.Equal(new EstimatorRates(3.5f, 4.2f, 3.0f, 1.0f), EstimatorRates.Default);

    [Fact]
    public void Characters_are_unicode_scalars_not_utf16_units() =>
        Assert.Equal(2, TokenEstimator.Estimate("😀😀", TextClass.Cjk, EstimatorRates.Default));

    [Fact]
    public void A_rate_never_goes_below_a_tenth() =>
        Assert.Equal(
            20,
            TokenEstimator.Estimate("ab", TextClass.Code, new EstimatorRates(Code: 0))
        );

    [Fact]
    public void The_estimator_section_overrides_only_what_it_sets()
    {
        using var doc = JsonDocument.Parse("""{"prose": 2.0, "json": "fast"}""");
        Assert.Equal(new EstimatorRates(Prose: 2.0f), EstimatorRates.From(doc.RootElement));
        Assert.Equal(EstimatorRates.Default, EstimatorRates.From(null));
    }

    [Fact]
    public void Tokens_saved_is_never_negative()
    {
        Assert.Equal(7, TokenEstimator.TokensSaved(10, 3));
        Assert.Equal(0, TokenEstimator.TokensSaved(3, 10));
    }
}
