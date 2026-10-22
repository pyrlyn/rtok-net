namespace Rtok.Tasks;

/// <summary>A claim was refused: the task is finished, blocked or another agent's. A claim without an id tries the next ready task.</summary>
public sealed class ClaimConflictException : TaskException
{
    /// <summary>Creates an exception with no message.</summary>
    public ClaimConflictException() { }

    /// <summary>Creates an exception.</summary>
    /// <param name="message">What went wrong, ready to print.</param>
    public ClaimConflictException(string message)
        : base(message) { }

    /// <summary>Creates an exception with a cause.</summary>
    /// <param name="message">What went wrong, ready to print.</param>
    /// <param name="inner">The cause.</param>
    public ClaimConflictException(string message, Exception inner)
        : base(message, inner) { }
}
