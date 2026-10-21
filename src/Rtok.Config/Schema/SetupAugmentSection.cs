namespace Rtok.Config;

/// <summary>the Auggie CLI's user settings file</summary>
/// <remarks>Read from [setup.augment].</remarks>
public sealed class SetupAugmentSection
{
    /// <summary>Auggie CLI: user settings, mcpServers</summary>
    public string SettingsPath { get; set; } = "~/.augment/settings.json";
}
