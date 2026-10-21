namespace Pyrlyn.Trash;

/// <summary>The OS refused to move an item to the trash; the item is still where it was.</summary>
public sealed class TrashException : IOException
{
    /// <summary>Creates the exception.</summary>
    public TrashException() { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    public TrashException(string message)
        : base(message) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The cause.</param>
    public TrashException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="hresult">The OS error code.</param>
    public TrashException(string message, int hresult)
        : base(message, hresult) { }
}
