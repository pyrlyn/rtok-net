namespace Rtok.Tasks;

/// <summary>The id is already used by a task the adapter holds: allocation raced or the counter fell behind.</summary>
public sealed class TaskTakenException : TaskException
{
    /// <summary>Creates an exception with no message.</summary>
    public TaskTakenException() { }

    /// <summary>Creates an exception.</summary>
    /// <param name="message">What went wrong, ready to print.</param>
    public TaskTakenException(string message)
        : base(message) { }

    /// <summary>Creates an exception with a cause.</summary>
    /// <param name="message">What went wrong, ready to print.</param>
    /// <param name="inner">The cause.</param>
    public TaskTakenException(string message, Exception inner)
        : base(message, inner) { }
}
