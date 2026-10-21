using Rtok.Plugin.Sdk;

namespace Rtok.Core;

/// <summary>The token estimate every saving is priced with: characters over a per-class rate,
/// rounded up.</summary>
public static class TokenEstimator
{
    /// <summary>Tokens in <paramref name="text"/>: Unicode scalars over the class's rate (never
    /// below 0.1), rounded up; empty text is 0.</summary>
    public static int Estimate(string text, TextClass textClass, EstimatorRates rates)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(rates);
        if (text.Length == 0)
        {
            return 0;
        }

        var perToken = textClass switch
        {
            TextClass.Prose => rates.Prose,
            TextClass.Json => rates.Json,
            TextClass.Cjk => rates.Cjk,
            _ => rates.Code,
        };
        var chars = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            chars++;
        }

        return (int)MathF.Ceiling(chars / MathF.Max(perToken, 0.1f));
    }

    /// <summary>Tokens saved by shortening <paramref name="before"/> to
    /// <paramref name="after"/>; never negative.</summary>
    public static int TokensSaved(int before, int after) => Math.Max(0, before - after);
}
