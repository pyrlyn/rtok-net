namespace Pyrlyn.RotatingLog;

/// <summary>Where a <see cref="RotatingFileLog"/> writes and how far it may grow.</summary>
public sealed class RotatingFileLogOptions
{
    /// <summary>The live file. Rotated siblings are <c>&lt;path&gt;.1</c>, <c>&lt;path&gt;.2</c>, …</summary>
    public required string Path { get; init; }

    /// <summary>Rotate once the next line would make the live file longer than this. Default 1 MiB.</summary>
    public long MaxBytes { get; init; } = 1_048_576;

    /// <summary>How many rotated siblings to keep. <c>0</c> truncates the live file and keeps none. Default 5.</summary>
    public int Files { get; init; } = 5;

    /// <summary>
    /// The floor: <c>error</c>, <c>warn</c>, <c>info</c> or <c>debug</c>, any case. A quieter
    /// line is dropped before any IO; an unknown name ranks as <c>error</c>. Default <c>info</c>.
    /// </summary>
    public string Level { get; init; } = "info";

    /// <summary>
    /// Copy every <c>error</c> line to <c>errors.log</c> beside <see cref="Path"/>, rotated the
    /// same way. Default true.
    /// </summary>
    public bool CopyErrors { get; init; } = true;

    /// <summary>
    /// How long a write waits for another writer before it drops its line. Default 1 s.
    /// </summary>
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The clock that stamps lines. Default <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
