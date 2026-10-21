namespace Rtok.Process;

/// <summary>A delegate that reads lines like a parser, for a one-off format.</summary>
/// <param name="feed">Reads one line.</param>
public sealed class CallbackProgress(Func<ChildOutput, string, ProgressUpdate?> feed)
    : IProgressParser
{
    /// <inheritdoc/>
    public ProgressUpdate? Feed(ChildOutput stream, string line) => feed(stream, line);
}
