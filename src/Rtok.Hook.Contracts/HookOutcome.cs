namespace Rtok.Hook.Contracts;

/// <summary>What one exchange with the resident came to.</summary>
public enum HookOutcome
{
    /// <summary>The resident answered; the reply is its stdout.</summary>
    Served,

    /// <summary>No resident listens.</summary>
    Unreachable,

    /// <summary>The resident runs another version and exits.</summary>
    RefusedVersion,

    /// <summary>The resident runs with another <see cref="HookFingerprint"/>.</summary>
    RefusedFingerprint,

    /// <summary>The budget ran out, or the connection broke or answered nonsense.</summary>
    Failed,
}
