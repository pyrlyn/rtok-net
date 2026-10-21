using Rtok.Plugin.Sdk;

namespace Rtok.Agents.Hosts;

/// <summary>A host rtok installs into: its apps, and what each module can do there (rtok's host folders).</summary>
/// <param name="Id">The id on the command line: <c>claude</c>, <c>codex</c>, <c>vscode</c>.</param>
/// <param name="Variants">Its apps, in <c>agents list</c> order.</param>
/// <param name="Hooks">rtok's hook entries.</param>
/// <param name="Mcp">rtok's MCP server entry.</param>
/// <param name="Proxy">The model base URL pointed at rtok's proxy.</param>
/// <param name="Plugin">rtok's plugin tree, linked or installed.</param>
/// <param name="Shared">Every app reads the same files, so one install covers them all.</param>
/// <param name="PluginSurfaces">The rtok surfaces the host's plugin serves.</param>
public sealed record AgentHost(
    string Id,
    IReadOnlyList<HostVariant> Variants,
    ByKind Hooks,
    ByKind Mcp,
    ByKind Proxy,
    ByKind Plugin,
    bool Shared = false,
    IReadOnlyList<Surface>? PluginSurfaces = null
)
{
    /// <summary>The module names, in print order.</summary>
    public static IReadOnlyList<string> Modules { get; } = ["hooks", "mcp", "proxy", "plugin"];

    /// <summary>The support of module <paramref name="module"/> on an app of <paramref name="kind"/>.</summary>
    /// <exception cref="ArgumentException">There is no such module.</exception>
    public Support SupportOf(string module, HostKind kind) =>
        module switch
        {
            "hooks" => Hooks.Of(kind),
            "mcp" => Mcp.Of(kind),
            "proxy" => Proxy.Of(kind),
            "plugin" => Plugin.Of(kind),
            _ => throw new ArgumentException($"no module {module}", nameof(module)),
        };

    /// <summary>
    /// Whether an app of <paramref name="kind"/> reaches rtok's <paramref name="surface"/>: through
    /// the module that serves it (hooks for hook and CLI, MCP, proxy), or through the plugin.
    /// </summary>
    public bool Reaches(HostKind kind, Surface surface)
    {
        var module = surface switch
        {
            Surface.Hook or Surface.Cli => Hooks,
            Surface.Mcp => Mcp,
            Surface.Proxy => Proxy,
            _ => Support.No(""),
        };
        return module.Of(kind).Level != SupportLevel.No
            || (
                PluginSurfaces?.Contains(surface) == true
                && Plugin.Of(kind).Level != SupportLevel.No
            );
    }
}
