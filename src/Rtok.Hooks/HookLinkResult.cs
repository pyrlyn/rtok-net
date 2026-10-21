namespace Rtok.Hooks;

/// <summary>What <see cref="HookClientLink.Ensure"/> did.</summary>
public enum HookLinkResult
{
    /// <summary>Created <c>rtok-hook</c> beside the first <c>rtok</c> on PATH.</summary>
    Linked,

    /// <summary>Re-pointed a dangling or other-version <c>rtok-hook</c> link there.</summary>
    Relinked,

    /// <summary>The link is already there and points at this build's client.</summary>
    AlreadyLinked,

    /// <summary>No <c>rtok</c> on PATH.</summary>
    NotOnPath,

    /// <summary>The first <c>rtok</c> on PATH is another binary; its install owns that directory.</summary>
    NotRunningExe,

    /// <summary>No <c>rtok-hook</c> beside the running <c>rtok</c>.</summary>
    NoClient,

    /// <summary>A regular file or a link to something else holds the name; it is left alone.</summary>
    Foreign,

    /// <summary>Links are not made on this OS (Windows installs ship both binaries).</summary>
    Unsupported,

    /// <summary>The file system refused.</summary>
    Failed,
}
