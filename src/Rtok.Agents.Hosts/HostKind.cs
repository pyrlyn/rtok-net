namespace Rtok.Agents.Hosts;

/// <summary>How a host app runs: a terminal binary or a desktop application.</summary>
public enum HostKind
{
    /// <summary>A terminal binary.</summary>
    Cli,

    /// <summary>A desktop application, an IDE or an IDE extension.</summary>
    Desktop,
}
