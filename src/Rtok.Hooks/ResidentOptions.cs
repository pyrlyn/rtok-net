using Rtok.Hook.Contracts;

namespace Rtok.Hooks;

/// <summary>Tuning for tests; the defaults are the product's.</summary>
public sealed class ResidentOptions
{
    /// <summary>How often the resident checks its lock and socket and replays the queue.</summary>
    public TimeSpan Tick { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How long a new resident waits for an exiting one's lock.</summary>
    public TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How long one connection may take to send its request.</summary>
    public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The version served; others are refused.</summary>
    public string Version { get; init; } = HookWire.Version;

    /// <summary>The fingerprint served; others are refused.</summary>
    public ulong Fingerprint { get; init; } = HookFingerprint.Current();
}
