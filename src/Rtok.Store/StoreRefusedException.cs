namespace Rtok.Store;

/// <summary>The file at <see cref="Path"/> is not a store this rtok-net can use: another program
/// made it (the Rust rtok's <c>rtok.db</c>) or a newer rtok-net migrated it (README N15). The
/// file is left untouched.</summary>
public sealed class StoreRefusedException : Exception
{
    public StoreRefusedException(string path, string reason)
        : base(
            $"{path}: {reason}. Move the file away to start a new store; rtok-net does not import it."
        )
    {
        Path = path;
        Reason = reason;
    }

    public StoreRefusedException() { }

    public StoreRefusedException(string message)
        : base(message) { }

    public StoreRefusedException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>The refused database file.</summary>
    public string Path { get; } = "";

    /// <summary>Why it was refused.</summary>
    public string Reason { get; } = "";
}
