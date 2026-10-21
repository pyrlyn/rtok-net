namespace Rtok.Agents.Hosts;

/// <summary>A module's support on a host's CLI app and on its desktop app.</summary>
/// <param name="Cli">On the CLI app.</param>
/// <param name="Desktop">On the desktop app.</param>
public readonly record struct ByKind(Support Cli, Support Desktop)
{
    /// <summary>The same support on every app.</summary>
    public static implicit operator ByKind(Support both) => new(both, both);

    /// <summary>The support on an app of <paramref name="kind"/>.</summary>
    public Support Of(HostKind kind) => kind == HostKind.Cli ? Cli : Desktop;
}
