namespace Rtok.Config;

/// <summary>Zed's `settings.json` carries `context_servers`</summary>
/// <remarks>Read from [setup.zed].</remarks>
public sealed class SetupZedSection
{
    /// <summary>Zed's `settings.json` carries `context_servers`</summary>
    public string ConfigPath { get; set; } = "~/.config/zed/settings.json";
}
