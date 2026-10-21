namespace Pyrlyn.Config.Layered;

/// <summary>A configuration file that cannot be read or edited, with the place that failed.</summary>
public sealed class ConfigFileException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="file">The file, or null for text without one.</param>
    /// <param name="line">The 1-based line.</param>
    /// <param name="column">The 1-based column, or 0.</param>
    /// <param name="reason">What is wrong.</param>
    public ConfigFileException(string? file, int line, int column, string reason)
        : base(Format(file, line, column, reason))
    {
        File = file;
        Line = line;
        Column = column;
        Reason = reason;
    }

    /// <summary>Creates the exception without a location.</summary>
    public ConfigFileException()
        : this(null, 0, 0, "invalid configuration file") { }

    /// <summary>Creates the exception without a location.</summary>
    /// <param name="message">What is wrong.</param>
    public ConfigFileException(string message)
        : this(null, 0, 0, message) { }

    /// <summary>Creates the exception without a location.</summary>
    /// <param name="message">What is wrong.</param>
    /// <param name="innerException">The cause.</param>
    public ConfigFileException(string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = message;
    }

    /// <summary>The file, or null.</summary>
    public string? File { get; }

    /// <summary>The 1-based line.</summary>
    public int Line { get; }

    /// <summary>The 1-based column, or 0.</summary>
    public int Column { get; }

    /// <summary>What is wrong, without the location.</summary>
    public string Reason { get; }

    private static string Format(string? file, int line, int column, string reason)
    {
        var at = column > 0 ? $"{line}:{column}" : $"{line}";
        return $"{file ?? "<text>"}:{at}: {reason}";
    }
}
