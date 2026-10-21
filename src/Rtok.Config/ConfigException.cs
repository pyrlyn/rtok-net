namespace Rtok.Config;

/// <summary>A config command that cannot do what was asked; the message says why.</summary>
public sealed class ConfigException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ConfigException() { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong, for the user.</param>
    public ConfigException(string message)
        : base(message) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong, for the user.</param>
    /// <param name="innerException">The cause.</param>
    public ConfigException(string message, Exception innerException)
        : base(message, innerException) { }
}
