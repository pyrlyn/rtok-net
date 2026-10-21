namespace Rtok.Store;

/// <summary>How <see cref="RtokStore.Open"/> opens the database file.</summary>
/// <param name="DbPath">The SQLite file; its directory is created owner-only.</param>
public sealed record StoreOptions(string DbPath)
{
    /// <summary>Where call bodies over <see cref="CallIoInlineBytes"/> are archived. Without it
    /// (the hook path) such a body keeps only its size.</summary>
    public string? ArchiveDir { get; init; }

    /// <summary><c>[core] call_io_inline_bytes</c>: the largest body kept inline in <c>call_io</c>.</summary>
    public int CallIoInlineBytes { get; init; } = 65536;

    /// <summary><c>[core] store_raw</c>: save request bodies verbatim instead of cleaned.</summary>
    public bool StoreRaw { get; init; }

    /// <summary>How long a statement waits for another writer's lock in steady state.</summary>
    public TimeSpan Busy { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The longer wait for the migration and maintenance windows.</summary>
    public TimeSpan MaintenanceBusy { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How many times opening retries a locked file before it gives up.</summary>
    public int ConnectAttempts { get; init; } = 10;

    /// <summary>The clock behind every <c>ts</c> column.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
