namespace Rtok.Config;

/// <summary>Antigravity: the IDE plugin root, where rtok links `plugins/antigravity`, and the CLI's staged-plugin root, written only by `agy plugin install`</summary>
/// <remarks>Read from [setup.antigravity].</remarks>
public sealed class SetupAntigravitySection
{
    /// <summary>Antigravity 2.0 / IDE: plugins/antigravity linked here</summary>
    public string PluginsPath { get; set; } = "~/.gemini/config/plugins";

    /// <summary>agy plugin install stages here; read only</summary>
    public string CliPluginsPath { get; set; } = "~/.gemini/antigravity-cli/plugins";
}
