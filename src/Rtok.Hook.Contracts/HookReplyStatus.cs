namespace Rtok.Hook.Contracts;

/// <summary>How the resident answered a request.</summary>
public enum HookReplyStatus : byte
{
    /// <summary>Served: the hook's stdout follows.</summary>
    Served = 0,

    /// <summary>Another version: the resident exits, a new one is due.</summary>
    RefusedVersion = 1,

    /// <summary>Another <see cref="HookFingerprint"/>: the resident would read other inputs.</summary>
    RefusedFingerprint = 2,
}
