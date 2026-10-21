namespace Rtok.Agents.Usage;

/// <summary>
/// The token legs and estimated cost of one table row. <see cref="CostUsd"/> is null when no
/// model in the row has a price: <c>-</c> in the table, <c>null</c> in JSON, never <c>$0.00</c>
/// (that means free).
/// </summary>
public sealed class UsageRow
{
    /// <summary>All four legs.</summary>
    public long Tokens { get; private set; }

    /// <summary>Uncached input tokens.</summary>
    public long Input { get; private set; }

    /// <summary>Tokens written to the prompt cache.</summary>
    public long CacheWrite { get; private set; }

    /// <summary>Tokens read from the prompt cache.</summary>
    public long CacheRead { get; private set; }

    /// <summary>Output tokens, reasoning included.</summary>
    public long Output { get; private set; }

    /// <summary>The estimate in USD, rounded to a millionth; null when nothing in the row has a price.</summary>
    public double? CostUsd { get; private set; }

    internal void Add(UsageSlice slice, double? cost)
    {
        Input += slice.Input;
        CacheWrite += slice.CacheCreate;
        CacheRead += slice.CacheRead;
        Output += slice.Output;
        Tokens += slice.Tokens;
        if (cost is { } c)
        {
            CostUsd = (CostUsd ?? 0) + c;
        }
    }

    internal UsageRow Rounded()
    {
        CostUsd = CostUsd is { } c ? UsageCost.Round6(c) : null;
        return this;
    }
}
