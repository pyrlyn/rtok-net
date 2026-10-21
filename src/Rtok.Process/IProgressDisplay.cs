namespace Rtok.Process;

/// <summary>Draws indicators on a terminal. A surface that owns one installs it in <see cref="ProgressDisplay"/>.</summary>
public interface IProgressDisplay
{
    /// <summary>A spinner labelled with an action and its target, present tense, ending in <c>…</c>.</summary>
    /// <param name="label">The label.</param>
    /// <returns>The spinner.</returns>
    IIndicator Spinner(string label);

    /// <summary>A bar over <paramref name="total"/> items, drawn above the spinners started after it.</summary>
    /// <param name="label">The label.</param>
    /// <param name="total">The number of items.</param>
    /// <param name="unit">What the items are.</param>
    /// <returns>The bar.</returns>
    IIndicator Bar(string label, long total, ProgressUnit unit);

    /// <summary>Hides every live indicator while <paramref name="write"/> runs (a prompt), and redraws them after.</summary>
    /// <param name="write">The work.</param>
    void Suspend(Action write);
}
