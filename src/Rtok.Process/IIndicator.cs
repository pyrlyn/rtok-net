namespace Rtok.Process;

/// <summary>What a person sees while work runs: a spinner, or a bar once the total is known.</summary>
public interface IIndicator
{
    /// <summary>A measurable step: a total turns a spinner into a bar.</summary>
    /// <param name="update">The step.</param>
    void Report(ProgressUpdate update);

    /// <summary>Replaces the label.</summary>
    /// <param name="message">The new label.</param>
    void Message(string message);

    /// <summary>Clears the indicator while <paramref name="write"/> writes to the terminal, then redraws it.</summary>
    /// <param name="write">The write.</param>
    void Suspend(Action write);

    /// <summary>Clears the indicator for good.</summary>
    void Finish();
}
