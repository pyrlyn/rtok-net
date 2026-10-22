namespace Rtok.Plugins.Docs;

/// <summary>A docs tool or fetch that cannot answer; the message says why, for the agent or user.</summary>
public sealed class DocsException : Exception
{
    /// <summary>Creates the exception.</summary>
    public DocsException() { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    public DocsException(string message)
        : base(message) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public DocsException(string message, Exception innerException)
        : base(message, innerException) { }
}
