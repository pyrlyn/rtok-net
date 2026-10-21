using System.Text.Json;

namespace Rtok.Core;

/// <summary>Characters per token by text class: the <c>[estimator]</c> config section.</summary>
public sealed record EstimatorRates(
    float Code = 3.5f,
    float Prose = 4.2f,
    float Json = 3.0f,
    float Cjk = 1.0f
)
{
    /// <summary>The defaults, about ±15% against real tokenizers.</summary>
    public static EstimatorRates Default { get; } = new();

    /// <summary>The rates in <paramref name="section"/> (<c>code</c>, <c>prose</c>,
    /// <c>json</c>, <c>cjk</c>), the default for any it lacks.</summary>
    public static EstimatorRates From(JsonElement? section) =>
        section is not { ValueKind: JsonValueKind.Object } s
            ? Default
            : new(
                Rate(s, "code", Default.Code),
                Rate(s, "prose", Default.Prose),
                Rate(s, "json", Default.Json),
                Rate(s, "cjk", Default.Cjk)
            );

    private static float Rate(JsonElement section, string name, float fallback) =>
        section.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetSingle(out var rate)
            ? rate
            : fallback;
}
