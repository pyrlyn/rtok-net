namespace Rtok.Hooks;

/// <summary>Why <see cref="HookResident.Run"/> returned.</summary>
public enum ResidentExit
{
    /// <summary>Another resident holds this home's lock.</summary>
    AlreadyRunning,

    /// <summary>The caller cancelled.</summary>
    Cancelled,

    /// <summary>A client of another version came: a resident of that version is due.</summary>
    VersionRefused,

    /// <summary>The lock file or the socket vanished: someone asked this resident to go.</summary>
    LockLost,
}
