using System.IO.Abstractions;
using Pyrlyn.Config.Layered;
using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Config;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>The host context of this machine for a loaded config: <c>rtok agents</c> and the
/// operator model's Hosts page build it here.</summary>
public static class HostContexts
{
    /// <summary>The context: this machine's home and disk, <c>[setup]</c>'s switches and the MCP
    /// files the user moved.</summary>
    /// <param name="loaded">The loaded config.</param>
    /// <param name="ask">Asks the user; null where nobody can answer.</param>
    /// <returns>The context.</returns>
    public static HostContext Of(LoadedConfig loaded, Prompt? ask)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var setup = loaded.Config.Setup;
        var fs = new FileSystem();
        var paths = SetupPaths.Of(
            key =>
                loaded.TryGet(key, out var entry)
                && entry.Source.Kind != ConfigSourceKind.Defaults
                && entry.Value is string path
                    ? path
                    : null,
            fs
        );
        var settings = new HostSettings(
            setup.DryRun,
            setup.Yes,
            setup.Backup,
            (int)Math.Min(setup.BackupFiles, int.MaxValue),
            setup.Mcp,
            ask
        );
        return new HostContext(
            HostEnvironment.Current(),
            new McpDisk(fs, HostFile.Write),
            HostCommands.Run,
            RtokBinary.Current,
            settings,
            paths
        );
    }
}
