namespace Rtok.Hook.Contracts;

/// <summary>One hook call as <c>rtok-hook</c> hands it to the resident.</summary>
/// <param name="Version">The client's version; the resident serves only its own.</param>
/// <param name="Fingerprint">The client's <see cref="HookFingerprint"/>; the resident serves only its own.</param>
/// <param name="Event">The event argument, empty when the host passed none.</param>
/// <param name="Host"><c>--host</c>, empty when not given.</param>
/// <param name="Cwd">The client's working directory; the hook runs for it.</param>
/// <param name="HostEnv">The client's <see cref="HookFingerprint.HostVariables"/> that are set.</param>
/// <param name="Pid">The client's pid, so the resident can read its ancestors.</param>
/// <param name="Stdin">The host's payload.</param>
public sealed record HookRequest(
    string Version,
    ulong Fingerprint,
    string Event,
    string Host,
    string Cwd,
    IReadOnlyList<KeyValuePair<string, string>> HostEnv,
    int Pid,
    ReadOnlyMemory<byte> Stdin
)
{
    /// <summary>The request this process would send: its version, environment, cwd and pid.</summary>
    public static HookRequest Current(string hookEvent, string host, ReadOnlyMemory<byte> stdin) =>
        new(
            HookWire.Version,
            HookFingerprint.Current(),
            hookEvent,
            host,
            Environment.CurrentDirectory,
            HookFingerprint.CurrentHostEnv(),
            Environment.ProcessId,
            stdin
        );
}
