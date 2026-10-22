namespace Rtok.Tasks;

/// <summary>A refusal or a failure of a task operation; the message is for the user as is.</summary>
public class TaskException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public TaskException() { }

    /// <summary>Creates an exception.</summary>
    /// <param name="message">What went wrong, ready to print.</param>
    public TaskException(string message)
        : base(message) { }

    /// <summary>Creates an exception with a cause.</summary>
    /// <param name="message">What went wrong, ready to print.</param>
    /// <param name="inner">The cause.</param>
    public TaskException(string message, Exception inner)
        : base(message, inner) { }
}
