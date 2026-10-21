namespace Pyrlyn.Jsonc;

/// <summary>A document that is not JSONC. The message reads <c>file:line:column: reason</c>.</summary>
public sealed class JsoncException : FormatException
{
    /// <summary>A parse failure without a position.</summary>
    public JsoncException()
        : this("not JSONC") { }

    /// <summary>A parse failure without a position.</summary>
    public JsoncException(string message)
        : this(message, null) { }

    /// <summary>A parse failure without a position.</summary>
    public JsoncException(string message, Exception? innerException)
        : base(message, innerException) => Reason = message;

    /// <summary>A parse failure at <paramref name="line"/> and <paramref name="column"/> (both 1-based).</summary>
    public JsoncException(
        string? file,
        long line,
        long column,
        string reason,
        Exception? inner = null
    )
        : base($"{file ?? "<jsonc>"}:{line}:{column}: {reason}", inner)
    {
        File = file;
        Line = line;
        Column = column;
        Reason = reason;
    }

    /// <summary>The file the text came from, when the caller named one.</summary>
    public string? File { get; }

    /// <summary>The 1-based line of the failure.</summary>
    public long Line { get; }

    /// <summary>The 1-based byte column of the failure.</summary>
    public long Column { get; }

    /// <summary>What is wrong there.</summary>
    public string Reason { get; }
}
