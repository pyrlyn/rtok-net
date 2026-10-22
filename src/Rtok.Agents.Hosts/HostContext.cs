using System.IO.Abstractions;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts;

/// <summary>What one <c>rtok agents</c> run reads and writes through: a test passes a fake home and an in-memory disk.</summary>
public sealed class HostContext
{
    /// <summary>Builds the context.</summary>
    /// <param name="env">The home, OS and environment.</param>
    /// <param name="disk">Where host files are read and written.</param>
    /// <param name="run">Runs <c>&lt;bin&gt; --version</c>.</param>
    /// <param name="rtok">The rtok binary entries may name.</param>
    /// <param name="settings">The run's switches.</param>
    /// <param name="paths">MCP files the user moved, keyed <c>&lt;host&gt;.&lt;surface&gt;</c> (<see cref="Mcp.SetupPaths"/>).</param>
    /// <param name="hookPaths">Hook files the user moved, keyed the same way (<see cref="Mcp.SetupPaths.HooksOf"/>).</param>
    public HostContext(
        HostEnvironment env,
        McpDisk disk,
        HostCommand run,
        RtokBinary rtok,
        HostSettings settings,
        IReadOnlyDictionary<string, string>? paths = null,
        IReadOnlyDictionary<string, string>? hookPaths = null
    )
    {
        HookPaths = hookPaths ?? new Dictionary<string, string>(StringComparer.Ordinal);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(rtok);
        Env = env;
        Disk = disk;
        Rtok = rtok;
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Probe = new HostProbe(env, disk.Fs, run);
        RtokOnPath = Probe.FindOnPath("rtok") is not null;
        // Off Windows a missing `rtok` on PATH is reported, not worked around (rtok_command).
        var command =
            RtokOnPath || env.Os != McpOs.Windows
                ? McpServer.Rtok.Command
                : rtok.Self ?? McpServer.Rtok.Command;
        McpEnv = new McpHostEnv(
            env.Home,
            env.Os,
            env.Variable("APPDATA"),
            command,
            rtok.Self,
            paths
        );
    }

    /// <summary>The home, OS and environment.</summary>
    public HostEnvironment Env { get; }

    /// <summary>Where host files are read and written.</summary>
    public McpDisk Disk { get; }

    /// <summary>The file system.</summary>
    public IFileSystem Fs => Disk.Fs;

    /// <summary>The rtok binary entries may name.</summary>
    public RtokBinary Rtok { get; }

    /// <summary>The run's switches.</summary>
    public HostSettings Settings { get; }

    /// <summary>Finds apps and asks their version.</summary>
    public HostProbe Probe { get; }

    /// <summary>The host table's inputs: home, OS, how entries name rtok, the user's paths.</summary>
    public McpHostEnv McpEnv { get; }

    /// <summary>Hook files the user moved, keyed <c>&lt;host&gt;.&lt;surface&gt;</c>.</summary>
    public IReadOnlyDictionary<string, string> HookPaths { get; }

    /// <summary>A bare <c>rtok</c> is on <c>PATH</c>, so entries that spawn it by name work.</summary>
    public bool RtokOnPath { get; }

    /// <summary>The command entries spawn: <c>rtok</c>, or on Windows without it on <c>PATH</c> the running binary.</summary>
    public string Command => McpEnv.Command;
}
