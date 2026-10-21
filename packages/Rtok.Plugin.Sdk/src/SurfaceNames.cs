namespace Rtok.Plugin.Sdk;

/// <summary>The lower-case surface names used in the <c>rtok plugins</c> table and config keys.</summary>
public static class SurfaceNames
{
    private static readonly (Surface Surface, string Name)[] Ordered =
    [
        (Surface.Hook, "hook"),
        (Surface.Mcp, "mcp"),
        (Surface.Proxy, "proxy"),
        (Surface.Cli, "cli"),
    ];

    /// <summary>The name of each surface in <paramref name="surfaces"/>, in the order hook, mcp,
    /// proxy, cli.</summary>
    public static IReadOnlyList<string> Of(Surface surfaces) =>
        [.. Ordered.Where(s => surfaces.HasFlag(s.Surface)).Select(s => s.Name)];
}
