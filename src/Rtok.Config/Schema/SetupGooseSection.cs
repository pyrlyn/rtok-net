namespace Rtok.Config;

/// <summary>Goose's `config.yaml`, where `extensions.rtok` goes</summary>
/// <remarks>Read from [setup.goose].</remarks>
public sealed class SetupGooseSection
{
    /// <summary>Goose CLI and Desktop: extensions.rtok; Windows: %APPDATA%/Block/goose/config/config.yaml; GOOSE_PATH_ROOT: &lt;root&gt;/config/config.yaml</summary>
    public string ConfigPath { get; set; } = "~/.config/goose/config.yaml";
}
