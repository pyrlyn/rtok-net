namespace Rtok.Process;

/// <summary>
/// A bar over a known number of items that rtok works through itself (worktrees to remove,
/// agents to install). It counts finished items: <see cref="Step"/> finishes the previous one
/// and names the next. Commands run inside it keep their own spinner under the bar.
/// </summary>
public sealed class Batch
{
    private readonly IIndicator indicator;
    private readonly ProgressUnit unit;
    private bool running;

    internal Batch(IIndicator indicator, long total, ProgressUnit unit)
    {
        this.indicator = indicator;
        this.unit = unit;
        Total = total;
    }

    /// <summary>Items finished so far.</summary>
    public long Position { get; private set; }

    /// <summary>The number of items.</summary>
    public long Total { get; }

    /// <summary>A batch that draws nothing, for a caller that does not own the terminal but shares the code path of one that does.</summary>
    /// <param name="total">The number of items.</param>
    /// <param name="unit">What the items are.</param>
    /// <returns>The batch.</returns>
    public static Batch Hidden(long total, ProgressUnit unit) =>
        new(HiddenIndicator.Instance, total, unit);

    /// <summary>The previous item is done; <paramref name="message"/> names the one that starts now.</summary>
    /// <param name="message">The item.</param>
    public void Step(string message)
    {
        Complete();
        running = true;
        Draw(message);
    }

    /// <summary>The last item is done; the bar is cleared.</summary>
    public void Finish()
    {
        Complete();
        Draw(null);
        indicator.Finish();
    }

    private void Complete()
    {
        if (running)
        {
            running = false;
            Position = Math.Min(Position + 1, Total);
        }
    }

    private void Draw(string? message) =>
        indicator.Report(new ProgressUpdate(Position, Total, unit, message));
}
