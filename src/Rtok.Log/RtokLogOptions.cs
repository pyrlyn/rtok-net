using Rtok.Store;

namespace Rtok.Log;

/// <summary>What <see cref="RtokLog.Create"/> takes besides <c>[log]</c>.</summary>
public sealed record RtokLogOptions
{
    /// <summary>Opens the store for the <c>to_db</c> sink, once, on the first row; null writes no rows.</summary>
    public Func<RtokStore>? OpenStore { get; init; }

    /// <summary>The <c>RTOK_LOG</c> value (<see cref="LogLevels.Debug"/>); null reads the variable.</summary>
    public string? Debug { get; init; }

    /// <summary>Where the <c>RTOK_LOG</c> lines go instead of stderr (tests).</summary>
    public TextWriter? DebugWriter { get; init; }

    /// <summary>The clock of the file's stamps.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
