namespace Rtok.Config;

/// <summary>One `[stats.prices."&lt;model&gt;"]` row — USD per MTok</summary>
/// <remarks>Read from [stats.prices.&lt;name&gt;].</remarks>
public sealed class ModelPrice
{
    /// <summary>USD per million input tokens</summary>
    public double Input { get; set; }

    /// <summary>USD per million tokens written to the prompt cache</summary>
    public double CacheWrite { get; set; }

    /// <summary>USD per million tokens read from the prompt cache</summary>
    public double CacheRead { get; set; }

    /// <summary>USD per million output tokens</summary>
    public double Output { get; set; }

    /// <summary>
    /// The shipped rows, sorted by model. A <c>@batch</c> or <c>@flex</c> suffix is the price of
    /// usage that ran on that tier.
    /// </summary>
    /// <returns>A new table.</returns>
    public static IDictionary<string, ModelPrice> Defaults() =>
        new Dictionary<string, ModelPrice>(StringComparer.Ordinal)
        {
            ["claude-fable-5-1"] = Row(10.0, 12.5, 0.25, 50.0),
            ["claude-fable-5-1@batch"] = Row(5.0, 6.25, 0.125, 25.0),
            ["claude-haiku-4-5"] = Row(1.0, 1.25, 0.1, 5.0),
            ["claude-haiku-4-5@batch"] = Row(0.5, 0.625, 0.05, 2.5),
            ["claude-opus-5-5"] = Row(4.0, 5.0, 0.2, 20.0),
            ["claude-opus-5-5@batch"] = Row(2.0, 2.5, 0.1, 10.0),
            ["claude-sonnet-5"] = Row(2.0, 2.5, 0.2, 10.0),
            ["claude-sonnet-5-5"] = Row(2.0, 2.5, 0.1, 10.0),
            ["claude-sonnet-5-5@batch"] = Row(1.0, 1.25, 0.05, 5.0),
            ["claude-sonnet-5@batch"] = Row(1.0, 1.25, 0.1, 5.0),
            ["gpt-5"] = Row(1.25, 1.25, 0.125, 10.0),
            ["gpt-5-mini"] = Row(0.25, 0.25, 0.025, 2.0),
            ["gpt-5-mini@batch"] = Row(0.125, 0.125, 0.0125, 1.0),
            ["gpt-5-mini@flex"] = Row(0.125, 0.125, 0.0125, 1.0),
            ["gpt-5@batch"] = Row(0.625, 0.625, 0.0625, 5.0),
            ["gpt-5@flex"] = Row(0.625, 0.625, 0.0625, 5.0),
        };

    private static ModelPrice Row(
        double input,
        double cacheWrite,
        double cacheRead,
        double output
    ) =>
        new()
        {
            Input = input,
            CacheWrite = cacheWrite,
            CacheRead = cacheRead,
            Output = output,
        };
}
