using System.IO.Abstractions;

namespace Rtok.Mcp.Install;

/// <summary>
/// The per-host MCP table: one <see cref="McpSpec"/> per host surface, as rtok's installers
/// write them (rtok T275, T277). A host whose entry lives in YAML or a TOML array (goose,
/// continue, reasonix) keeps its own adapter and is not here.
/// </summary>
public static class McpHosts
{
    /// <summary>Every host id the table has, in <c>agents list</c> order.</summary>
    public static IReadOnlyList<string> Ids { get; } =
    [.. McpHostRows.All.Select(r => r.Host).Distinct(StringComparer.Ordinal)];

    /// <summary>Every surface of every host.</summary>
    /// <param name="env">The home, OS and the user's path settings.</param>
    /// <param name="fs">The file system: paths are built with its rules, and a few hosts pick the file that exists.</param>
    public static IReadOnlyList<McpSpec> All(McpHostEnv env, IFileSystem fs)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(fs);
        var paths = new McpHostPaths(env, fs);
        return [.. McpHostRows.All.Select(r => r.Spec(env, paths))];
    }

    /// <summary>The surfaces of <paramref name="host"/>; empty for a host with no MCP entry here.</summary>
    public static IReadOnlyList<McpSpec> For(string host, McpHostEnv env, IFileSystem fs) =>
        [.. All(env, fs).Where(s => string.Equals(s.Host, host, StringComparison.Ordinal))];
}
