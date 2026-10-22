using Rtok.Agent.Sdk;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Proxy;

/// <summary>
/// The model base URL of one app pointed at rtok's proxy (rtok's <c>proxy</c> module): Claude
/// Code, Codex and aider with <c>--proxy</c>, OpenCode always. Every other host says why it has
/// none. Remove takes back a URL only while it is rtok's.
/// </summary>
internal sealed class ProxyModule(AgentHost host, HostVariant variant, HostContext ctx)
{
    public const string Name = "proxy";

    /// <summary>How far this app supports the proxy.</summary>
    public Support Support { get; } = host.Proxy.Of(variant.Kind);

    /// <summary>The file the base URL lives in on this app.</summary>
    public IReadOnlyList<OwnedFile> Files { get; } =
        host.Proxy.Of(variant.Kind).Level == SupportLevel.No ? [] : For(host, variant, ctx);

    /// <summary>Whether the base URL is rtok's.</summary>
    public bool Installed() => Files.Any(f => f.Installed(ctx.Disk));

    /// <summary>
    /// Whether <paramref name="mode"/> writes the base URL: a host that always takes it, or one
    /// behind <c>--proxy</c> when it is given, or on update when it reads back as rtok's already
    /// (rtok <c>carry_flags</c>). A URL on an old port is not rtok's any more, except Codex's
    /// provider table, so there <c>--proxy</c> moves it.
    /// </summary>
    public bool Wanted(McpMode mode) =>
        Support.Level == SupportLevel.Yes
        || Support.Level == SupportLevel.Flag
            && (ctx.Settings.Proxy || mode == McpMode.Update && Installed());

    /// <summary>Points the base URL at the proxy, or takes rtok's back; one report per file.</summary>
    /// <param name="mode">Install, update or remove.</param>
    /// <param name="wanted"><see cref="Wanted"/>, asked before anything was written.</param>
    /// <param name="apply">The run.</param>
    /// <exception cref="IOException">A file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">A file does not parse, or is not shaped as the host writes it.</exception>
    public IReadOnlyList<string> Apply(McpMode mode, bool wanted, Apply apply) =>
        mode != McpMode.Remove && !wanted
            ? []
            : [.. Files.Select(f => f.Apply(mode == McpMode.Remove, apply, ctx.Disk))];

    private static IReadOnlyList<OwnedFile> For(AgentHost host, HostVariant v, HostContext ctx)
    {
        var root = ctx.Settings.ProxyUrl;
        var openAi = root + "/v1";
        var port = new Uri(root).Port;
        string? McpPath() =>
            McpHosts
                .For(host.Id, ctx.McpEnv, ctx.Fs)
                .FirstOrDefault(s =>
                    string.Equals(s.Surface, v.SurfaceName, StringComparison.Ordinal)
                )
                ?.Path;
        OwnedFile? file = host.Id switch
        {
            "claude" => new EnvProxy(
                ctx.PathOf(ctx.HookPaths, "claude.cli", "~/.claude/settings.json"),
                "ANTHROPIC_BASE_URL",
                root,
                port
            ),
            "codex" when McpPath() is { } path => new CodexProxy(path, openAi),
            "opencode" when McpPath() is { } path => new EnvProxy(
                path,
                "OPENAI_BASE_URL",
                openAi,
                port
            ),
            "aider" => new AiderProxy(
                ctx.PathOf(ctx.ProxyPaths, "aider.cli", "~/.aider.conf.yml"),
                openAi,
                port
            ),
            _ => null,
        };
        return file is null ? [] : [file];
    }
}
