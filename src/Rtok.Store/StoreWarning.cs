namespace Rtok.Store;

/// <summary>A housekeeping step that failed and waits for the next start.</summary>
public sealed record StoreWarning(string What, string Message);
