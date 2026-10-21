namespace Rtok.Process;

/// <summary>Any line holding <c>NN%</c> becomes a 0–100 bar.</summary>
public sealed class PercentProgress : IProgressParser
{
    /// <inheritdoc/>
    public ProgressUpdate? Feed(ChildOutput stream, string line) =>
        ProgressText.PercentIn(line) is var (value, _, _)
            ? new ProgressUpdate(value, 100, ProgressUnit.Percent, null)
            : null;
}
