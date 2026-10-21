namespace Rtok.Process;

/// <summary>Draws nothing.</summary>
public sealed class HiddenIndicator : IIndicator
{
    /// <summary>The one instance.</summary>
    public static HiddenIndicator Instance { get; } = new();

    private HiddenIndicator() { }

    /// <inheritdoc/>
    public void Report(ProgressUpdate update) { }

    /// <inheritdoc/>
    public void Message(string message) { }

    /// <inheritdoc/>
    public void Suspend(Action write)
    {
        ArgumentNullException.ThrowIfNull(write);
        write();
    }

    /// <inheritdoc/>
    public void Finish() { }
}
