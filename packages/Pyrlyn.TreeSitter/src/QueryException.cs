namespace Pyrlyn.TreeSitter;

/// <summary>A query that did not compile, with the position of the error in the query text.</summary>
public sealed class QueryException : Exception
{
    /// <summary>A query error.</summary>
    public QueryException(QueryErrorKind kind, int row, int column, int offset, string message)
        : base($"query {kind} error at {row + 1}:{column + 1}: {message}")
    {
        Kind = kind;
        Row = row;
        Column = column;
        Offset = offset;
        Detail = message;
    }

    /// <summary>Not used; a query error always carries its kind and position.</summary>
    public QueryException()
        : this(QueryErrorKind.Syntax, 0, 0, 0, "") { }

    /// <summary>Not used; a query error always carries its kind and position.</summary>
    public QueryException(string message)
        : this(QueryErrorKind.Syntax, 0, 0, 0, message) { }

    /// <summary>Not used; a query error always carries its kind and position.</summary>
    public QueryException(string message, Exception innerException)
        : base(message, innerException) => Detail = message;

    /// <summary>What went wrong.</summary>
    public QueryErrorKind Kind { get; }

    /// <summary>Zero-based line of the error in the query text.</summary>
    public int Row { get; }

    /// <summary>Zero-based column of the error.</summary>
    public int Column { get; }

    /// <summary>Byte offset of the error.</summary>
    public int Offset { get; }

    /// <summary>The offending name, or the offending line with a caret under the error.</summary>
    public string Detail { get; }
}
