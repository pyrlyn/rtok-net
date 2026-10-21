namespace Pyrlyn.CommandLine.Docs;

/// <summary>A help page the parser does not understand. The reader stops; it never guesses.</summary>
public sealed class HelpFormatException : Exception
{
    /// <summary>Creates the exception.</summary>
    public HelpFormatException() { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What was not understood, and where.</param>
    public HelpFormatException(string message)
        : base(message) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What was not understood, and where.</param>
    /// <param name="inner">The cause.</param>
    public HelpFormatException(string message, Exception inner)
        : base(message, inner) { }
}
