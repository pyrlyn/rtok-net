namespace Rtok.Config;

/// <summary>Continue's user `config.yaml`, where the `rtok` item of `mcpServers` goes</summary>
/// <remarks>Read from [setup.continue].</remarks>
public sealed class SetupContinueSection
{
    /// <summary>Continue (VS Code, JetBrains, cn): mcpServers item rtok; Windows: %USERPROFILE%/.continue/; CONTINUE_GLOBAL_DIR moves it</summary>
    public string ConfigPath { get; set; } = "~/.continue/config.yaml";
}
