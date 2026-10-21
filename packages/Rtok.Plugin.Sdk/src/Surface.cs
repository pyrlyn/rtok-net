namespace Rtok.Plugin.Sdk;

/// <summary>Where a plugin is reachable from (rtok D2). A plugin may answer on several.</summary>
[Flags]
public enum Surface
{
    /// <summary>Reachable from nowhere.</summary>
    None = 0,

    /// <summary><c>rtok hook &lt;event&gt;</c>.</summary>
    Hook = 1,

    /// <summary><c>rtok mcp</c>.</summary>
    Mcp = 2,

    /// <summary><c>rtok proxy</c>.</summary>
    Proxy = 4,

    /// <summary>A subcommand such as <c>rtok run</c> or <c>rtok stats</c>.</summary>
    Cli = 8,
}
