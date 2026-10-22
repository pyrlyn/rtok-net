using System.IO.Abstractions;
using Pyrlyn.Config.Layered;
using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Agents.Hosts.Restart;
using Rtok.Config;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>The host context of this machine for a loaded config: <c>rtok agents</c> and the
/// operator model's Hosts page build it here.</summary>
public static class HostContexts
{
    /// <summary>The context: this machine's home and disk, <c>[setup]</c>'s switches and the MCP
    /// hook and proxy files the user moved.</summary>
    /// <param name="loaded">The loaded config.</param>
    /// <param name="ask">Asks the user; null where nobody can answer.</param>
    /// <param name="warn">Takes each warning line as it happens; null drops them.</param>
    /// <returns>The context.</returns>
    /// <remarks>A sandboxed run (<see cref="HostEnvironment.SandboxVariable"/>) sees no app running, so none is closed.</remarks>
    public static HostContext Of(LoadedConfig loaded, Prompt? ask, Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var setup = loaded.Config.Setup;
        var fs = new FileSystem();
        string? UserSet(string key) =>
            loaded.TryGet(key, out var entry)
            && entry.Source.Kind != ConfigSourceKind.Defaults
            && entry.Value is string path
                ? path
                : null;
        var settings = new HostSettings(
            setup.DryRun,
            setup.Yes,
            setup.Backup,
            (int)Math.Min(setup.BackupFiles, int.MaxValue),
            setup.Mcp,
            ask,
            (long)Math.Min(setup.HookTimeoutS, long.MaxValue),
            setup.Proxy,
            ProxyUrls.Root(loaded.Config.Proxy),
            warn
        );
        var env = HostEnvironment.Current();
        return new HostContext(
            env,
            new McpDisk(fs, HostFile.Write, HostFile.Delete),
            HostCommands.Run,
            RtokBinary.Current,
            settings,
            SetupPaths.Of(UserSet, fs),
            SetupPaths.HooksOf(UserSet, fs),
            SetupPaths.ProxyOf(UserSet, fs),
            env.Sandboxed ? null : new AppProcesses(env.Os)
        );
    }
}
