using ConsoleAppFramework;
using Rtok.Core;

namespace Rtok.Cli;

/// <summary>The linked plugins.</summary>
internal sealed class PluginsCommands
{
    private readonly TextWriter _stdout = Console.Out;

    /// <summary>List plugins: id, kind, enabled, surfaces.</summary>
    /// <param name="json">Print JSON.</param>
    [Command("plugins")]
    public void Plugins(bool json = false)
    {
        // Until the config feeds the registry, nothing overrides a plugin's default.
        var registry = new PluginRegistry(LinkedPlugins.Create(), _ => null);
        _stdout.Write(json ? PluginsTable.RenderJson(registry) : PluginsTable.Render(registry));
    }
}
