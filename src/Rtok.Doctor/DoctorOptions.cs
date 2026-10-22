using Rtok.Agents.Hosts;
using Rtok.Config;

namespace Rtok.Doctor;

/// <summary>What one doctor run reads through: a test passes a fake home and fake probes.</summary>
/// <param name="Hosts">The home, disk and host table.</param>
/// <param name="Config">The loaded config.</param>
/// <param name="Cwd">The working directory: project files and <c>.mcp.json</c> are read from here.</param>
public sealed record DoctorOptions(HostContext Hosts, RtokConfig Config, string Cwd)
{
    /// <summary>The checkout root (a linked worktree's own), where instruction files live; <see cref="Cwd"/> when null.</summary>
    public string? Project { get; init; }

    /// <summary>Asks an MCP server for its tools; by default nothing is started and every count is 0.</summary>
    public McpProbe Mcp { get; init; } = static (_, _) => null;

    /// <summary>Asks a proxy for its upstream; by default nothing answers.</summary>
    public HealthProbe Health { get; init; } = static (_, _) => null;

    /// <summary>How <c>--fix</c> writes; <see cref="DoctorWrites.Disk"/> on a real machine.</summary>
    public FixWrite? Write { get; init; }

    /// <summary>Managed settings files the host enforces; the OS's standard place when null.</summary>
    public IReadOnlyList<string>? Managed { get; init; }

    /// <summary>The running rtok binary as entries may name it, besides a bare <c>rtok</c>.</summary>
    public RtokBinary Rtok => Hosts.Rtok;
}
