namespace Rtok.Mcp.Install;

/// <summary>What <see cref="McpInstaller.Apply"/> does to rtok's entry.</summary>
public enum McpMode
{
    /// <summary>Write the entry.</summary>
    Install,

    /// <summary>Write the entry again: the same as <see cref="Install"/> (D33).</summary>
    Update,

    /// <summary>Take the entry out, if it is still rtok's.</summary>
    Remove,
}
