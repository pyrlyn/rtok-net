namespace Rtok.Process;

/// <summary>
/// git's <c>Receiving objects:  45% (120/267), 1.2 MiB | 3 MiB/s</c> and
/// <c>Updating files: 50% (1/2)</c>: the object or file count as the bar, the phase as the
/// message. Pass <c>--progress</c> where the subcommand has it: git draws these only on a terminal.
/// </summary>
public sealed class GitProgress : IProgressParser
{
    /// <inheritdoc/>
    public ProgressUpdate? Feed(ChildOutput stream, string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (ProgressText.PercentIn(line) is not var (percent, start, end))
        {
            return null;
        }
        var phase = line[..start].TrimEnd().TrimEnd(':');
        if (phase.StartsWith("remote:", StringComparison.Ordinal))
        {
            phase = phase["remote:".Length..];
        }
        phase = phase.Trim();
        var message = phase.Length == 0 ? null : phase;
        return Counts(line[end..]) is var (done, total)
            ? new ProgressUpdate(done, total, ProgressUnit.Items, message)
            : new ProgressUpdate(percent, 100, ProgressUnit.Percent, message);
    }

    private static (long Done, long Total)? Counts(string rest)
    {
        rest = rest.TrimStart();
        if (!rest.StartsWith('('))
        {
            return null;
        }
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        var close = rest.IndexOf(')', StringComparison.Ordinal);
        return
            slash > 0
            && close > slash
            && ProgressText.TryCount(rest[1..slash], out var done)
            && ProgressText.TryCount(rest[(slash + 1)..close], out var total)
            ? (done, total)
            : null;
    }
}
