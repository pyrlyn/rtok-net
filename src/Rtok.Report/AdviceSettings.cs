using Rtok.Config;
using Rtok.Measure;
using Rtok.Store;

namespace Rtok.Report;

/// <summary>The config values the advice rules compare the ledgers with.</summary>
public sealed record AdviceSettings(
    double ExpandMaxRate,
    long InjectBudget,
    long KeepTurns,
    long MinTokens,
    double CodeRate,
    string ReadMode
)
{
    /// <summary>The settings of <paramref name="config"/>.</summary>
    public static AdviceSettings Of(RtokConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new(
            config.Expand.MaxRate,
            config.Plugins.Inject.BudgetTokens,
            config.Plugins.Archive.KeepTurns,
            config.Plugins.Archive.MinTokens,
            config.Estimator.Code,
            config.Plugins.Read.DefaultMode
        );
    }
}
