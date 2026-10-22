namespace Rtok.Web;

/// <summary><c>rtok web</c> refused to start; the message says why and what to do.</summary>
public sealed class WebStartException : Exception
{
    /// <summary>Creates the exception.</summary>
    public WebStartException() { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Why, and what to do.</param>
    public WebStartException(string message)
        : base(message) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Why, and what to do.</param>
    /// <param name="inner">The cause.</param>
    public WebStartException(string message, Exception inner)
        : base(message, inner) { }
}
