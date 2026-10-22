namespace Rtok.Store;

/// <summary>Another connection holds the writer lock and <see cref="StoreOptions.FailFast"/>
/// gave up waiting for it.</summary>
public sealed class StoreLockedException : Exception
{
    public StoreLockedException()
        : base("database is locked") { }

    public StoreLockedException(string message)
        : base(message) { }

    public StoreLockedException(string message, Exception innerException)
        : base(message, innerException) { }
}
