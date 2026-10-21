using Rtok.Agent.Sdk;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Mcp;

/// <summary>
/// rtok's MCP entry on one app: the shared core's surfaces (<see cref="McpHosts"/>), or the
/// host's own file for Goose, Continue and Reasonix.
/// </summary>
internal sealed class McpModule(AgentHost host, HostVariant variant, HostContext ctx)
{
    public const string Name = "mcp";

    /// <summary>
    /// The core's surfaces of this app; of every app when the host is shared, since one apply
    /// covers them all (VS Code's stable and insiders).
    /// </summary>
    public IReadOnlyList<McpSpec> Specs { get; } =
        host.Mcp.Of(variant.Kind).Level == SupportLevel.No
            ? []
            :
            [
                .. McpHosts
                    .For(host.Id, ctx.McpEnv, ctx.Fs)
                    .Where(s =>
                        host.Shared
                        || string.Equals(s.Surface, variant.SurfaceName, StringComparison.Ordinal)
                    ),
            ];

    /// <summary>The host's own file when the core does not model it.</summary>
    public McpFile? File { get; } = OwnFile(host, variant, ctx);

    /// <summary>The files this module edits.</summary>
    public IEnumerable<string> Files =>
        Specs.Select(s => s.Path).Concat(File is null ? [] : [File.Path]);

    /// <summary>One status row per core surface; none for a host's own file.</summary>
    public IReadOnlyList<McpRow> Rows() =>
        [.. Specs.Select(s => McpStatus.Of(s, ctx.Disk, ctx.Rtok.Is, ctx.Env.Home))];

    /// <summary>Whether rtok's entry is there: on any surface (a stale one too, which update rewrites), or in the host's own file.</summary>
    public bool Installed() =>
        Specs.Count > 0 ? Rows().Any(r => r.Found) : File?.Installed(ctx.Disk, ctx.Rtok.Is) == true;

    /// <summary>Installs, updates or removes the entry; one report per file.</summary>
    /// <exception cref="IOException">A file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">A file does not parse, or rtok's entry cannot be put in or taken out of it.</exception>
    public IReadOnlyList<string> Apply(McpMode mode, Apply apply)
    {
        if (mode != McpMode.Remove && !ctx.Settings.Mcp)
        {
            return [Agent.Sdk.Apply.NoChanges];
        }
        List<string> reports =
        [
            .. Specs.Select(s => McpInstaller.Apply(s, mode, apply, ctx.Disk, ctx.Rtok.Is)),
        ];
        if (File is not null)
        {
            reports.Add(File.Apply(mode, apply, ctx.Disk, ctx.Rtok.Is, ctx.Command));
        }
        return reports;
    }

    private static McpFile? OwnFile(AgentHost host, HostVariant variant, HostContext ctx)
    {
        string Path(string fallback) =>
            ctx.McpEnv.Paths?.GetValueOrDefault($"{host.Id}.{variant.SurfaceName}")
                is { Length: > 0 } set
                ? set
                : ctx.Probe.Expand(fallback);
        return host.Id switch
        {
            "goose" => new GooseMcp(Path("~/.config/goose/config.yaml")),
            "continue" => new ContinueMcp(Path("~/.continue/config.yaml")),
            "reasonix" => new ReasonixMcp(Path("~/.reasonix/config.toml")),
            _ => null,
        };
    }
}
