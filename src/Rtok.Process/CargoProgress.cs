namespace Rtok.Process;

/// <summary>
/// cargo's own bar, <c>    Building [====>      ] 120/300: serde, rtok(bin)</c>: the crate count
/// as the bar, the crates being built as the message. Cargo draws it only on a terminal, so a
/// caller that pipes stderr sets <see cref="Environment"/> on the child.
/// </summary>
public sealed class CargoProgress : IProgressParser
{
    /// <summary>Makes cargo draw the bar into a pipe.</summary>
    public static IReadOnlyList<(string Name, string Value)> Environment { get; } =
    [("CARGO_TERM_PROGRESS_WHEN", "always"), ("CARGO_TERM_PROGRESS_WIDTH", "80")];

    /// <inheritdoc/>
    public ProgressUpdate? Feed(ChildOutput stream, string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var open = line.IndexOf('[', StringComparison.Ordinal);
        var close = open < 0 ? -1 : line.IndexOf(']', open + 1);
        if (close < 0 || line.AsSpan(open + 1, close - open - 1).IndexOfAnyExcept("=> ") >= 0)
        {
            return null;
        }
        var after = line[(close + 1)..];
        var colon = after.IndexOf(':', StringComparison.Ordinal);
        var (counts, crates) = colon < 0 ? (after, "") : (after[..colon], after[(colon + 1)..]);
        var slash = counts.IndexOf('/', StringComparison.Ordinal);
        if (
            slash < 0
            || !ProgressText.TryCount(counts[..slash].Trim(), out var done)
            || !ProgressText.TryCount(counts[(slash + 1)..].Trim(), out var total)
            || total == 0
        )
        {
            return null;
        }
        crates = crates.Trim();
        return new ProgressUpdate(
            done,
            total,
            ProgressUnit.Items,
            crates.Length == 0 ? null : crates
        );
    }
}
